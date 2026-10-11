using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ses_uyg;

internal static class AiRuntimeSmokeTests
{
    private static int Main()
    {
        try { Run().GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task Run()
    {
        using (var runtime = new AiRuntime())
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12)))
        {
            Console.WriteLine("Starting local AI engine. No microphone will be opened.");
            await runtime.Start(timeout.Token);
            Console.WriteLine("Server ready. Loading female voice.");
            await runtime.Client.PrepareFemaleVoice(12, timeout.Token);
            Console.WriteLine("Female model warmed up.");
            var samples = new byte[26460];
            double total = 0;
            long energy = 0;
            using (var output = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai-synthetic-output.pcm")))
            {
                for (int block = 0; block < 15; block++)
                {
                    for (int i = 0; i < samples.Length / 2; i++)
                    {
                        double t = (block * 13230 + i) / 44100.0;
                        short value = (short)(5000 * (Math.Sin(2 * Math.PI * 150 * t) + 0.4 * Math.Sin(2 * Math.PI * 300 * t)));
                        samples[2 * i] = (byte)value;
                        samples[2 * i + 1] = (byte)(value >> 8);
                    }
                    var watch = Stopwatch.StartNew();
                    var converted = await runtime.Client.ConvertAudio(samples, timeout.Token);
                    total += watch.Elapsed.TotalMilliseconds;
                    if (converted.SequenceEqual(samples)) throw new Exception("AI returned unchanged input.");
                    for (int i = 0; i < converted.Length; i += 2) energy += Math.Abs((int)BitConverter.ToInt16(converted, i));
                    output.Write(converted, 0, converted.Length);
                    Console.WriteLine("Block " + block + ": " + watch.ElapsedMilliseconds + " ms, " + converted.Length + " bytes");
                }
            }
            if (energy == 0) throw new Exception("AI returned only silence.");
            Console.WriteLine("PASS: real neural inference, nonzero converted PCM. Mean processing: " + (int)(total / 15) + " ms per 300 ms block.");
        }
    }
}
