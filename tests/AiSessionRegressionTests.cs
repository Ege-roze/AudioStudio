using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ses_uyg;

internal static class AiSessionRegressionTests
{
    private static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: AI queue ordering, capture-buffer ownership, overload, cancellation and error propagation."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Run()
    {
        var entered = new TaskCompletionSource<bool>();
        var release = new TaskCompletionSource<bool>();
        var done = new TaskCompletionSource<bool>();
        var received = new List<int>();
        Exception failure = null;
        using (var session = new AiVoiceSession(async (pcm, token) =>
        {
            entered.TrySetResult(true);
            await release.Task;
            return pcm;
        }, (pcm, ms) => { received.Add(pcm[0]); if (received.Count == 3) done.TrySetResult(true); }, ex => failure = ex))
        {
            var pcm = new byte[] { 1, 0 };
            session.Enqueue(pcm, pcm.Length);
            await entered.Task;
            pcm[0] = 99; // WaveIn reuses its callback buffer.
            session.Enqueue(new byte[] { 2, 0 }, 2);
            session.Enqueue(new byte[] { 3, 0 }, 2);
            bool overflow = false;
            try { session.Enqueue(new byte[] { 4, 0 }, 2); } catch (InvalidOperationException) { overflow = true; }
            Check(overflow, "Overload must stop the stream, not grow an unbounded queue.");
            release.SetResult(true);
            Check(await Task.WhenAny(done.Task, Task.Delay(3000)) == done.Task, "Audio queue did not drain.");
            Check(received.SequenceEqual(new[] { 1, 2, 3 }), "Captured buffers changed or reordered.");
            Check(failure == null, "Unexpected queue failure.");
        }
        var waiting = new TaskCompletionSource<bool>();
        var late = new TaskCompletionSource<byte[]>();
        int delivered = 0;
        var cancelled = new AiVoiceSession((pcm, token) => { waiting.SetResult(true); return late.Task; }, (pcm, ms) => delivered++, ex => failure = ex);
        cancelled.Enqueue(new byte[2], 2);
        await waiting.Task;
        cancelled.Dispose();
        late.SetResult(new byte[2]);
        await cancelled.Completion;
        Check(delivered == 0, "A stopped session delivered stale audio.");
        var error = new TaskCompletionSource<Exception>();
        using (var broken = new AiVoiceSession((pcm, token) => { throw new InvalidOperationException("inference failure"); }, (pcm, ms) => delivered++, ex => error.TrySetResult(ex)))
        {
            broken.Enqueue(new byte[2], 2);
            Check(await Task.WhenAny(error.Task, Task.Delay(3000)) == error.Task, "Backend errors must reach the UI.");
            Check((await error.Task).Message == "inference failure", "Backend error was lost.");
        }
    }
}
