using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;
using ses_uyg;

// Opt-in hardware check: real microphone and speaker monitoring, with no audio files saved.
internal static class AiPreviewDeviceSmokeTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var main = new Form1())
            {
                var handle = main.Handle;
                var type = typeof(Form1).Assembly.GetType("ses_uyg.AudioTestForm", true);
                var lease = typeof(Form1).Assembly.GetType("ses_uyg.AiPreviewLease", true);
                var acquire = Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(lease), main, typeof(Form1).GetMethod("AcquireAiPreview", Hidden));
                using (var test = (Form)Activator.CreateInstance(type, Hidden, null,
                    new object[] { new Func<bool>(() => { throw new Exception("Preview must not start main audio."); }), acquire }, null))
                {
                    typeof(Form1).GetField("audioTestForm", Hidden).SetValue(main, test);
                    test.Opacity = 0;
                    test.ShowInTaskbar = false;
                    test.Show();
                    var microphone = Field<ComboBox>(test, "aiTestMicrophone");
                    var output = Field<ComboBox>(test, "aiTestDestination");
                    if (microphone.SelectedIndex < 0 || output.SelectedIndex < 0)
                        throw new Exception("Hardware check requires an unambiguous microphone and listening device.");
                    Console.WriteLine("Microphone: " + microphone.SelectedItem);
                    Console.WriteLine("Listening device: " + output.SelectedItem);
                    Field<Button>(test, "aiTestStart").PerformClick();
                    var watch = Stopwatch.StartNew();
                    string previous = null;
                    while (Field<WaveInEvent>(test, "aiTestCapture") == null && watch.Elapsed < TimeSpan.FromMinutes(3))
                    {
                        Application.DoEvents();
                        string status = Field<Label>(test, "aiTestStatus").Text;
                        if (status != previous) { previous = status; Console.WriteLine(status); }
                        if (Field<object>(test, "aiTestCancellation") == null) throw new Exception("AI preview ended before capture: " + status);
                        Thread.Sleep(50);
                    }
                    if (Field<WaveInEvent>(test, "aiTestCapture") == null) throw new Exception("AI preview did not become ready.");
                    Console.WriteLine("Listening for 8 seconds. No audio will be saved.");
                    int inputPeak = 0, outputPeak = 0;
                    var outputDevice = Field<WaveOutEvent>(test, "aiTestPlayback");
                    watch.Restart();
                    while (watch.Elapsed < TimeSpan.FromSeconds(8))
                    {
                        Application.DoEvents();
                        if (Field<WaveInEvent>(test, "aiTestCapture") == null) throw new Exception(Field<Label>(test, "aiTestStatus").Text);
                        inputPeak = Math.Max(inputPeak, Field<int>(test, "aiTestInputPeak"));
                        outputPeak = Math.Max(outputPeak, Field<int>(test, "aiTestOutputPeak"));
                        Thread.Sleep(50);
                    }
                    int inputBlocks = Field<int>(test, "aiTestInputBlocks"), outputBlocks = Field<int>(test, "aiTestOutputBlocks");
                    Console.WriteLine("Captured blocks: " + inputBlocks + "; converted blocks: " + outputBlocks
                        + "; input peak: " + inputPeak + "%; AI peak: " + outputPeak + "%; played bytes: " + outputDevice.GetPosition());
                    if (inputBlocks == 0 || outputBlocks == 0 || outputDevice.GetPosition() == 0)
                        throw new Exception("The live microphone / conversion / playback path did not move audio.");
                    if (Field<object>(main, "waveIn") != null) throw new Exception("Main stream was incorrectly started.");
                    Field<Button>(test, "aiTestStop").PerformClick();
                    watch.Restart();
                    while (Field<object>(test, "aiTestCancellation") != null && watch.Elapsed < TimeSpan.FromSeconds(15))
                    {
                        Application.DoEvents();
                        Thread.Sleep(50);
                    }
                    if (Field<object>(test, "aiTestCancellation") != null || Field<WaveInEvent>(test, "aiTestCapture") != null)
                        throw new Exception("Preview did not release its microphone.");
                    Console.WriteLine("PASS: real preview start button, model, microphone, output driver and Stop; main streaming stayed off.");
                }
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name, Hidden).GetValue(value); }
}
