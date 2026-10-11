using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ses_uyg;

internal static class AiVoicesSmokeTests
{
    private static int Main(string[] args)
    {
        try { Run(args).GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task Run(string[] args)
    {
        var store = AiVoiceStore.Default();
        bool fresh = args.Contains("--fresh");
        if (fresh)
        {
            string original = store.ModelPath(AiVoiceCatalog.All[1]);
            store = new AiVoiceStore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice-fresh-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(store.DirectoryPath);
            File.Copy(original, Path.Combine(store.DirectoryPath, ".voice-1.part"));
        }
        Console.WriteLine("Model directory: " + store.DirectoryPath);
        var selected = fresh ? new[] { AiVoiceCatalog.All[1] } : args.Length == 0 ? AiVoiceCatalog.All : AiVoiceCatalog.All.Where(v => args.Contains(v.Id)).ToArray();
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
        {
            foreach (var voice in selected)
            {
                Console.WriteLine("Download/verify: " + voice.Name + " (" + voice.Bytes + " bytes)");
                await store.EnsureAsync(voice, null, timeout.Token);
                if (!store.IsInstalled(voice)) throw new Exception("Voice not installed: " + voice.Name);
            }
            using (var runtime = new AiRuntime())
            {
                Console.WriteLine("Starting engine. No microphone or speaker will be opened.");
                await runtime.Start(store.DirectoryPath, timeout.Token);
                foreach (var voice in fresh ? selected : selected.Concat(new[] { AiVoiceCatalog.All[0] }))
                {
                    Console.WriteLine("Prepare: " + voice.Name);
                    await runtime.Client.PrepareVoice(voice, 12, timeout.Token);
                    byte[] pcm = new byte[44100 * voice.BufferMilliseconds / 1000 * 2];
                    long energy = 0;
                    var watch = Stopwatch.StartNew();
                    for (int block = 0; block < 3; block++)
                    {
                        for (int i = 0; i < pcm.Length / 2; i++)
                        {
                            double t = (block * pcm.Length / 2 + i) / 44100.0;
                            short value = (short)(5000 * (Math.Sin(2 * Math.PI * 150 * t) + .4 * Math.Sin(2 * Math.PI * 300 * t)));
                            pcm[i * 2] = (byte)value; pcm[i * 2 + 1] = (byte)(value >> 8);
                        }
                        byte[] converted = await runtime.Client.ConvertAudio(pcm, timeout.Token);
                        if (converted.SequenceEqual(pcm)) throw new Exception("Unchanged audio: " + voice.Name);
                        for (int i = 0; i < converted.Length; i += 2) energy += Math.Abs((int)BitConverter.ToInt16(converted, i));
                    }
                    if (energy == 0) throw new Exception("Silent output: " + voice.Name);
                    Console.WriteLine("PASS " + voice.Name + ": verified model, changed nonzero PCM, " + watch.ElapsedMilliseconds / 3 + " ms / " + voice.BufferMilliseconds + " ms audio");
                }
            }
        }
        if (fresh) { CheckOnlySelected(store); Directory.Delete(store.DirectoryPath, true); }
    }
    private static void CheckOnlySelected(AiVoiceStore store)
    {
        if (Directory.Exists(Path.Combine(store.DirectoryPath, "0"))) throw new Exception("Default voice was downloaded without selection");
        Console.WriteLine("PASS: fresh installation works with Amitaro alone; default voice was not downloaded.");
    }
}
