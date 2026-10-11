using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NAudio.Wave;
using ses_uyg;

internal static class AudioPerformanceTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    private static int Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Application.EnableVisualStyles();
            string preferences = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "performance-preferences-" + Guid.NewGuid().ToString("N"));
            using (var form = (Form1)Activator.CreateInstance(typeof(Form1), Hidden, null, new object[] { preferences }, null))
            using (var input = new WaveInEvent()) // Never initialized or started.
            {
                var handle = form.Handle;
                var output = new BufferedWaveProvider(new WaveFormat(44100, 1)) { DiscardOnBufferOverflow = false };
                Set(form, "waveIn", input); Set(form, "waveProvider", output);
                var process = (Action<object, WaveInEventArgs>)Delegate.CreateDelegate(typeof(Action<object, WaveInEventArgs>), form,
                    typeof(Form1).GetMethod("WaveIn_DataAvailable", Hidden));
                Run(form, input, output, process, "Classic / neutral", 1f, false);
                Run(form, input, output, process, "Classic / pitch 1.7 + echo + EQ + distortion", 1.7f, true);
                Run(form, input, output, process, "Classic / pitch 0.7 + echo + EQ + distortion", .7f, true);
                Set(form, "waveIn", null); Set(form, "waveProvider", null);
            }
            Console.WriteLine("PASS: 6000 audio blocks processed (10 minutes of synthetic input); no physical devices opened or settings saved.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Run(Form1 form, WaveInEvent input, BufferedWaveProvider output, Action<object, WaveInEventArgs> process,
        string name, float pitch, bool effects)
    {
        Call(form, "SetPitch", pitch);
        Call(form, "SetEchoDelay", effects ? 52920 : 0);
        Call(form, "SetDistortion", effects ? 5f : 1f);
        Set(form, "eqBassGain", effects ? 10f : 0f);
        Set(form, "eqMidGain", effects ? -6f : 0f);
        Set(form, "eqTrebleGain", effects ? 10f : 0f);
        Call(form, "UpdateFilters");
        var signal = new byte[8820];
        for (int i = 0; i < signal.Length / 2; i++)
        {
            short value = (short)(6000 * Math.Sin(2 * Math.PI * 180 * i / 44100));
            signal[2 * i] = (byte)value; signal[2 * i + 1] = (byte)(value >> 8);
        }
        var work = new byte[signal.Length];
        var args = new WaveInEventArgs(work, work.Length);
        // Prime the JIT, pitch FFT and bounded echo queue before sampling memory.
        for (int i = 0; i < 30; i++) { Buffer.BlockCopy(signal, 0, work, 0, work.Length); process(input, args); output.ClearBuffer(); }
        var timings = new double[2000];
        long memoryBefore = GC.GetTotalMemory(true);
        for (int i = 0; i < timings.Length; i++)
        {
            Buffer.BlockCopy(signal, 0, work, 0, work.Length);
            long start = Stopwatch.GetTimestamp();
            process(input, args);
            timings[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            if (output.BufferedBytes != work.Length) throw new Exception("Missing audio or accumulating output: " + name);
            output.ClearBuffer();
            if (i % 100 == 0) Application.DoEvents();
            if (!ReferenceEquals(typeof(Form1).GetField("waveIn", Hidden).GetValue(form), input)) throw new Exception("Audio processing stopped: " + name);
        }
        long retained = GC.GetTotalMemory(true) - memoryBefore;
        var ordered = timings.OrderBy(value => value).ToArray();
        double p95 = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1];
        Console.WriteLine("PERF {0}: blocks={1}, audio=100ms, mean={2:F2}ms, p95={3:F2}ms, max={4:F2}ms, retained-managed={5:F2}MB",
            name, timings.Length, timings.Average(), p95, timings.Max(), retained / 1000000.0);
        if (p95 >= 100 || retained > 4 * 1024 * 1024) throw new Exception("Audio latency or retained-memory regression: " + name);
        var echo = (Queue<float>)typeof(Form1).GetField("echoBufferQueue", Hidden).GetValue(form);
        if (echo.Count > 52920) throw new Exception("Unbounded echo buffer.");
    }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Hidden).SetValue(target, value); }
    private static void Call(object target, string name, params object[] args) { target.GetType().GetMethod(name, Hidden).Invoke(target, args); }
}
