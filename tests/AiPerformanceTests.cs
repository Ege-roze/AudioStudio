using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ses_uyg;

internal static class AiPerformanceTests
{
    private static int Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        try { Run().GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static async Task Run()
    {
        var store = AiVoiceStore.Default();
        // Installed models only: this benchmark must not unexpectedly download gigabytes.
        foreach (var voice in AiVoiceCatalog.All)
            if (!store.IsInstalled(voice)) throw new Exception("Install this voice before benchmarking: " + voice.Name);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
        using (var runtime = new AiRuntime())
        {
            Console.WriteLine("AI benchmark: installed models, synthetic PCM, no physical audio devices.");
            var boot = Stopwatch.StartNew();
            await runtime.Start(store.DirectoryPath, timeout.Token);
            Console.WriteLine("Engine startup: {0:F2} s", boot.Elapsed.TotalSeconds);
            foreach (var voice in AiVoiceCatalog.All)
            {
                Console.WriteLine("Preparing " + voice.Name);
                await runtime.Client.PrepareVoice(voice, 12, timeout.Token);
                byte[] pcm = Signal(voice.BufferMilliseconds);
                int count = voice.BufferMilliseconds > 1000 ? 30 : 100;
                var timings = new List<double>();
                long energy = 0;
                for (int block = 0; block < count; block++)
                {
                    var watch = Stopwatch.StartNew();
                    byte[] output = await runtime.Client.ConvertAudio(pcm, timeout.Token);
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                    if (output.Length != pcm.Length || output.SequenceEqual(pcm)) throw new Exception("Invalid conversion: " + voice.Name);
                    for (int i = 0; i < output.Length; i += 2) energy += Math.Abs((int)BitConverter.ToInt16(output, i));
                }
                if (energy == 0) throw new Exception("Silent conversion: " + voice.Name);
                Report(voice.Name, timings, voice.BufferMilliseconds);
            }
            // Return to the first model and use the same queue as live capture.
            await runtime.Client.PrepareVoice(AiVoiceCatalog.All[0], 12, timeout.Token);
            await PacedStream(runtime.Client, timeout.Token);
        }
        Console.WriteLine("PASS: all model conversions, model switching, and paced streaming completed; owned engine disposed.");
    }

    private static async Task PacedStream(AiVoiceClient client, CancellationToken token)
    {
        const int count = 200, interval = 300;
        byte[] pcm = Signal(interval);
        var timings = new List<double>();
        var complete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int received = 0;
        var clock = Stopwatch.StartNew();
        Console.WriteLine("Starting 60-second stream: 200 blocks at real capture cadence.");
        using (var session = new AiVoiceSession(client.ConvertAudio,
            (output, milliseconds) =>
            {
                lock (timings) timings.Add(milliseconds);
                if (Interlocked.Increment(ref received) == count) complete.TrySetResult(true);
            }, error => complete.TrySetException(error)))
        {
            for (int i = 0; i < count; i++)
            {
                if (complete.Task.IsFaulted) await complete.Task;
                int delay = (int)((long)i * interval - clock.ElapsedMilliseconds);
                if (delay > 0) await Task.Delay(delay, token);
                session.Enqueue(pcm, pcm.Length);
                if (i > 0 && i % 50 == 0) Console.WriteLine("Stream progress: {0}/{1} sent, {2} returned", i, count, Volatile.Read(ref received));
            }
            if (await Task.WhenAny(complete.Task, Task.Delay(10000, token)) != complete.Task)
                throw new Exception("Timed out draining the live queue.");
            await complete.Task;
            session.Dispose();
            await session.Completion;
        }
        Report("Paced Tsukuyomi-chan", timings, interval);
        Console.WriteLine("PASS: {0}/{1} live blocks returned, no queue overload or conversion failure.", received, count);
    }

    private static void Report(string name, List<double> timings, int interval)
    {
        var ordered = timings.OrderBy(value => value).ToArray();
        double p95 = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1];
        Console.WriteLine("PERF {0}: blocks={1}, audio={2}ms, mean={3:F2}ms, p95={4:F2}ms, max={5:F2}ms, real-time-factor={6:F3}",
            name, timings.Count, interval, timings.Average(), p95, timings.Max(), timings.Average() / interval);
        if (p95 >= interval) throw new Exception("Insufficient real-time headroom: " + name);
    }

    private static byte[] Signal(int milliseconds)
    {
        byte[] pcm = new byte[44100 * milliseconds / 1000 * 2];
        for (int i = 0; i < pcm.Length / 2; i++)
        {
            double t = i / 44100.0;
            short value = (short)(5000 * (Math.Sin(2 * Math.PI * 150 * t) + .4 * Math.Sin(2 * Math.PI * 300 * t)));
            pcm[2 * i] = (byte)value; pcm[2 * i + 1] = (byte)(value >> 8);
        }
        return pcm;
    }
}
