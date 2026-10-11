using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using ses_uyg;

// Synthetic audio and isolated files only. Lifecycle checks use transparent windows; no device is opened.
internal static class AudioRegressionTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static string directory;
    private static string audioPath;

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "regression-data-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            audioPath = Path.Combine(directory, "ten-minutes.wav");
            WriteWave(audioPath, 600);
            AudioPanelAccess();
            IndependentAiTest();
            ApplicationLifetime();
            ApplicationPreferences();
            EchoAndConcurrency();
            StreamingAndCancellation();
            SessionRecovery();
            AiRouting();
            LibraryTransactions();
            Console.WriteLine("PASS: " + checks + " audio/library regression checks. No physical audio devices were used.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static Form1 NewForm(string settingsDirectory = null)
    {
        var form = (Form1)Activator.CreateInstance(typeof(Form1), Hidden, null,
            new object[] { settingsDirectory ?? Path.Combine(directory, "preferences-" + Guid.NewGuid().ToString("N")) }, null);
        Set(form, "configFilePath", Path.Combine(directory, Guid.NewGuid().ToString("N") + ".xml"));
        var handle = form.Handle; // Hidden handle for BeginInvoke and event-delivery tests.
        return form;
    }

    private static void ApplicationPreferences()
    {
        string preferences = Path.Combine(directory, "lifecycle-preferences");
        using (var form = NewForm(preferences))
        {
            Call(form, "SaveApplicationSettings", false, false, null, null, null);
            form.Close();
            Check(form.IsDisposed, "Disabling close-to-tray makes the title-bar X exit completely");
        }
        using (var form = NewForm(preferences))
        {
            Check(!(bool)Get<object>(form, "applicationSettings").GetType().GetProperty("CloseToTray").GetValue(Get<object>(form, "applicationSettings")),
                "The close preference survives reopening the application");
            Call(form, "SaveApplicationSettings", true, true, null, null, null);
        }
        using (var form = NewForm(preferences))
        {
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            Check(!form.Visible && Get<NotifyIcon>(form, "trayIcon").Visible && Get<object>(form, "waveIn") == null,
                "Start-in-tray uses saved preferences and does not activate the microphone");
            Get<ToolStripMenuItem>(form, "trayShow").PerformClick();
            Application.DoEvents();
            Check(form.Visible, "A background launch can be restored without hiding again");
            Call(form, "ExitApplication");
            Check(form.IsDisposed, "Explicit Exit overrides the close-to-tray preference");
        }
        using (var main = NewForm())
        {
            var microphones = Get<ComboBox>(main, "microphoneSelector");
            var outputs = Get<ComboBox>(main, "cableSelector");
            microphones.Items.Clear(); outputs.Items.Clear();
            microphones.Items.AddRange(new object[] { "Mic A", "Mic B" });
            outputs.Items.Add("CABLE Input");
            microphones.SelectedIndex = outputs.SelectedIndex = 0;
            var settingsType = typeof(Form1).Assembly.GetType("ses_uyg.SettingsForm", true);
            using (var settings = (Form)Activator.CreateInstance(settingsType, Hidden, null, new object[] { main }, null))
            {
                Set(main, "settingsForm", settings);
                var handle = settings.Handle;
                Get<CheckBox>(settings, "closeToTray").Checked = false;
                Get<ComboBox>(settings, "microphone").SelectedIndex = 1;
                settings.Close();
                Check(microphones.SelectedIndex == 0 && !File.Exists(Property<string>(Get<object>(main, "applicationSettingsStore"), "SettingsPath")),
                    "Closing Settings without Save preserves preferences and device selection");
            }
            using (var settings = (Form)Activator.CreateInstance(settingsType, Hidden, null, new object[] { main }, null))
            {
                Set(main, "settingsForm", settings);
                Get<ComboBox>(settings, "microphone").SelectedIndex = 1;
                Get<CheckBox>(settings, "startWithWindows").Enabled = false; // Never change the real startup registration in UI tests.
                Call(settings, "SaveSettings");
                Check(microphones.SelectedIndex == 1, "Saving Settings applies the selected microphone to the main stream");
            }
            using (var settings = (Form)Activator.CreateInstance(settingsType, Hidden, null, new object[] { main }, null))
            {
                Set(main, "settingsForm", settings);
                var session = ConfigureSession(main);
                Check(!Get<ComboBox>(settings, "microphone").Enabled && Get<CheckBox>(settings, "closeToTray").Enabled,
                    "During streaming, device choices lock while lifecycle settings remain editable");
                Call(main, "SaveApplicationSettings", false, false, null, "Mic A", "CABLE Input");
                Check(ReferenceEquals(Get<WaveInEvent>(main, "waveIn"), session) && microphones.SelectedIndex == 1,
                    "Changing lifecycle preferences cannot interrupt a stream or replace its device");
                Call(main, "btnStop_Click", main, EventArgs.Empty);
                Check(Get<ComboBox>(settings, "microphone").Enabled, "Stopping streaming unlocks the Settings device choices");
                using (var lease = (IDisposable)Call(main, "AcquireAiPreview"))
                    Check(!Get<ComboBox>(settings, "microphone").Enabled, "Independent AI preview also locks Settings device choices");
            }
            using (var selector = new ComboBox())
            {
                selector.Items.AddRange(new object[] { "Other microphone", "Mic B" });
                typeof(Form1).GetMethod("RestoreDeviceChoice", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { selector, "Mic B" });
                Check(selector.SelectedIndex == 1, "Remembered devices are restored by name rather than enumeration order");
                selector.Items.Clear();
                selector.Items.AddRange(new object[] { "Mic B", "Mic B" });
                typeof(Form1).GetMethod("RestoreDeviceChoice", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { selector, "Mic B" });
                Check(selector.SelectedIndex == -1, "Ambiguous device names do not silently choose a different microphone");
            }
        }
    }

    private static void ApplicationLifetime()
    {
        using (var preparation = new CancellationTokenSource())
        using (var form = NewForm())
        using (var editor = new Form { Opacity = 0, ShowInTaskbar = false })
        {
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.Show();
            editor.Show(form);
            var input = ConfigureSession(form);
            var output = Get<BufferedWaveProvider>(form, "waveProvider");
            Set(form, "aiPreparation", preparation);
            form.Close(); // The same UserClosing path as the title-bar X / Alt+F4.
            Check(!form.IsDisposed && !form.Visible && Get<NotifyIcon>(form, "trayIcon").Visible,
                "Title-bar close hides the application and exposes a tray icon");
            Check(!editor.IsDisposed && !editor.Visible, "Owned windows are hidden without losing their state");
            Check(ReferenceEquals(Get<WaveInEvent>(form, "waveIn"), input) && !preparation.IsCancellationRequested,
                "Hiding preserves the stream and pending AI preparation");
            Processor(form)(input, new WaveInEventArgs(new byte[882], 882));
            Check(output.BufferedBytes == 882, "Audio callbacks continue processing while the main window is hidden");
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 1;
            Check(Get<ToolStripMenuItem>(form, "trayShow").Text == "Show application"
                && Get<ToolStripMenuItem>(form, "trayExit").Text == "Exit application",
                "Tray actions follow the selected language");
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 0;
            Get<ToolStripMenuItem>(form, "trayShow").PerformClick();
            Check(form.Visible && editor.Visible && !Get<NotifyIcon>(form, "trayIcon").Visible,
                "The tray Show action restores the existing main and owned windows");
            Check(ReferenceEquals(Get<WaveInEvent>(form, "waveIn"), input), "Restoring does not restart audio");
            form.WindowState = FormWindowState.Maximized;
            form.Close();
            Get<ToolStripMenuItem>(form, "trayShow").PerformClick();
            Check(form.WindowState == FormWindowState.Maximized, "Restoring preserves a maximized window");
            form.Close();
            Get<ToolStripMenuItem>(form, "trayExit").PerformClick();
            Check(form.IsDisposed && editor.IsDisposed && preparation.IsCancellationRequested,
                "Tray Exit closes owned windows and cancels AI preparation");
            Check(Get<object>(form, "waveIn") == null && Get<object>(form, "waveOut") == null
                && Get<object>(form, "waveProvider") == null && Get<object>(form, "trayIcon") == null,
                "A full exit releases audio and tray resources");
        }

        using (var form = NewForm())
        {
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            Exception failure = null;
            form.Shown += (s, e) => form.BeginInvoke(new Action(() =>
            {
                try
                {
                    var exit = (Button)form.Controls.Find("exitApplicationButton", true).Single();
                    Check(exit.Enabled, "Explicit Exit remains available while streaming is stopped");
                    exit.PerformClick();
                    Check(form.IsDisposed, "The main Exit button closes rather than hides the app");
                }
                catch (Exception ex) { failure = ex; }
                finally { if (!form.IsDisposed) form.Dispose(); }
            }));
            Application.Run(form);
            if (failure != null) throw failure;
            Check(form.IsDisposed, "Explicit Exit terminates the main Windows message loop");
        }

        foreach (var reason in new[] { CloseReason.WindowsShutDown, CloseReason.TaskManagerClosing, CloseReason.ApplicationExitCall })
        {
            using (var form = NewForm())
            {
                ConfigureSession(form);
                var closing = new FormClosingEventArgs(reason, false);
                Call(form, "OnFormClosing", closing);
                Check(!closing.Cancel && Get<object>(form, "waveIn") == null && Get<object>(form, "trayIcon") == null,
                    reason + " performs cleanup without hiding or blocking closure");
            }
        }
    }

    private static void AiRouting()
    {
        using (var form = NewForm())
        {
            var input = ConfigureSession(form);
            var pending = new TaskCompletionSource<byte[]>();
            var entered = new TaskCompletionSource<bool>();
            var delivered = new TaskCompletionSource<bool>();
            var type = typeof(Form1).Assembly.GetType("ses_uyg.AiVoiceSession", true);
            Func<byte[], CancellationToken, Task<byte[]>> convert = (pcm, token) => { entered.TrySetResult(true); return pending.Task; };
            Action<byte[], double> deliver = (pcm, ms) => { Call(form, "ProcessMicrophoneAudio", input, pcm, pcm.Length); delivered.TrySetResult(true); };
            Action<Exception> failed = ex => { throw ex; };
            var ai = (IDisposable)Activator.CreateInstance(type, Hidden, null, new object[] { convert, deliver, failed }, null);
            Set(form, "aiSession", ai);
            var buffer = Get<BufferedWaveProvider>(form, "waveProvider");
            var microphone = new byte[882];
            microphone[0] = 0xe8; microphone[1] = 3; // 1000
            Processor(form)(input, new WaveInEventArgs(microphone, microphone.Length));
            Check(entered.Task.Wait(3000), "AI mode sends captured audio to the conversion worker");
            Check(buffer.BufferedBytes == 0, "Unprocessed microphone audio is never sent while AI is pending");
            var converted = new byte[882];
            converted[0] = 0xd0; converted[1] = 7; // 2000
            pending.SetResult(converted);
            Check(delivered.Task.Wait(3000), "Converted audio enters the existing effects/output pipeline");
            var result = new byte[882];
            buffer.Read(result, 0, result.Length);
            Check(Math.Abs(BitConverter.ToInt16(result, 0) - 2000) <= 1, "The output contains the AI-converted sample");
            Call(form, "StopAudio");
            Check(Get<object>(form, "aiSession") == null, "Stopping audio detaches the AI worker");
        }
    }

    private static void AudioPanelAccess()
    {
        using (var form = NewForm())
        {
            CheckAudioPanels(form, false, "Initial audio panels are locked");
            Get<ComboBox>(form, "cableSelector").Items.Clear();
            Call(form, "UpdateAiControls");
            Check(Get<Button>(form, "testAudioButton").Enabled && Get<Button>(form, "installCable").Enabled
                && Get<Button>(form, "uninstallCable").Enabled && Get<ComboBox>(form, "microphoneSelector").Enabled,
                "Test, VB-CABLE setup and device selection remain available before streaming");
            var item = new SoundItem { Name = "Access test", Path = audioPath };
            Check((bool)Call(form, "TryCommitSoundList", new List<SoundItem> { item }), "Prepare selected sound for access checks");
            Get<ListBox>(form, "listBoxSounds").SelectedIndex = 0;
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 1;
            CheckAudioPanels(form, false, "Selection and language changes cannot unlock stopped panels");
            Check(!Get<Button>(form, "playSound").Enabled && !Get<Button>(form, "aiEnableButton").Enabled,
                "Selected sound and AI actions inherit the panel lock");
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 0;
            Call(form, "ListBoxSounds_DoubleClick", form, EventArgs.Empty);
            Call(form, "EnableAi_Click", form, EventArgs.Empty);
            Check(Get<object>(form, "waveIn") == null && Get<object>(form, "playingSound") == null && Get<object>(form, "aiPreparation") == null,
                "Stopped playback and AI actions cannot implicitly start capture or installation");

            var input = ConfigureSession(form);
            CheckAudioPanels(form, true, "Starting a session unlocks all audio panels");
            Check(Get<TrackBar>(form, "trackBarPitch").Enabled && Get<Button>(form, "aiEnableButton").Enabled
                && Get<Button>(form, "playSound").Enabled && Get<Button>(form, "btnDeleteSound").Enabled,
                "Effects, AI and selected sound actions become usable during streaming");
            Call(form, "btnStop_Click", form, EventArgs.Empty);
            CheckAudioPanels(form, false, "Stopping locks audio panels again");
            Check(Get<Button>(form, "testAudioButton").Enabled && Get<Button>(form, "installCable").Enabled,
                "Stopping keeps Test and VB-CABLE setup available");

            input = ConfigureSession(form);
            using (var preparation = new CancellationTokenSource())
            {
                Set(form, "aiPreparation", preparation);
                Call(form, "UpdateAiControls");
                Check(Get<Button>(form, "aiDisableButton").Enabled && Get<Button>(form, "btnStop").Enabled,
                    "AI preparation can be cancelled while the ordinary audio stream continues");
                Call(form, "btnStop_Click", form, EventArgs.Empty);
                Check(preparation.IsCancellationRequested, "Stopping audio also cancels pending AI preparation");
                CheckAudioPanels(form, false, "Stop during AI preparation relocks the panels");
                Call(form, "RestartAudioForAi", input);
                Check(Get<object>(form, "waveIn") == null, "Late AI completion cannot restart a stopped stream");
                Set(form, "aiPreparation", null);
            }
            Call(form, "UpdateAiControls");
            Check(Get<Button>(form, "btnStart").Enabled && !Get<Button>(form, "aiEnableButton").Enabled,
                "AI cleanup restores Start while leaving stopped audio panels locked");

            var current = ConfigureSession(form);
            Call(form, "RestartAudioForAi", input);
            Check(ReferenceEquals(Get<WaveInEvent>(form, "waveIn"), current), "Late AI completion cannot replace a newer stream");
        }
    }

    private static void CheckAudioPanels(Form1 form, bool enabled, string description)
    {
        Check(new[] { "aiVoiceSection", "effectsSection", "soundLibrarySection" }
            .All(name => Get<Control>(form, name).Enabled == enabled), description);
    }

    private static void IndependentAiTest()
    {
        using (var main = NewForm())
        {
            int mainStartRequests = 0;
            var testType = typeof(Form1).Assembly.GetType("ses_uyg.AudioTestForm", true);
            var leaseType = typeof(Form1).Assembly.GetType("ses_uyg.AiPreviewLease", true);
            var acquire = Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(leaseType), main, typeof(Form1).GetMethod("AcquireAiPreview", Hidden));
            using (var test = (Form)Activator.CreateInstance(testType, Hidden, null,
                new object[] { new Func<bool>(() => { mainStartRequests++; return false; }), acquire }, null))
            {
                Set(main, "audioTestForm", test);
                var handle = test.Handle;
                Call(main, "UpdateAiControls");
                Get<ComboBox>(main, "cableSelector").Items.Clear();
                Get<ComboBox>(test, "source").Items.Clear();
                Check(Get<Button>(test, "aiTestStart").Enabled, "Independent AI test is available without a CABLE device or main stream");
                Check(Get<ComboBox>(test, "aiTestMicrophone").Items.Cast<object>().All(item => !item.ToString().Contains("CABLE"))
                    && Get<ComboBox>(test, "aiTestDestination").Items.Cast<object>().All(item => !item.ToString().Contains("CABLE")),
                    "Independent AI device lists exclude both ends of VB-CABLE");
                Get<ComboBox>(test, "aiTestMicrophone").SelectedIndex = -1;
                Pump((Task)Call(test, "StartAiTestAsync"));
                Check(Get<Label>(test, "aiTestStatus").Text.Contains("mikrofonunu") && !Get<bool>(main, "aiPreviewActive"),
                    "Missing AI test devices report a selection hint before reserving or starting the engine");

                ConfigureSession(main);
                Check(!Get<Button>(test, "aiTestStart").Enabled && Call(main, "AcquireAiPreview") == null,
                    "Main streaming prevents an independent AI session from taking over the engine");
                Call(main, "btnStop_Click", main, EventArgs.Empty);
                Check(Get<Button>(test, "aiTestStart").Enabled, "Stopping main streaming makes the independent test available again");
                Set(main, "installingCable", true);
                Check(Call(main, "AcquireAiPreview") == null, "AI preview cannot overlap driver maintenance");
                Set(main, "installingCable", false);
                using (var preparation = new CancellationTokenSource())
                {
                    Set(main, "aiPreparation", preparation);
                    Check(Call(main, "AcquireAiPreview") == null, "AI preview cannot overlap main AI preparation");
                    Set(main, "aiPreparation", null);
                }

                Set(main, "aiEnabled", true); // A model prepared by an earlier main stream.
                Get<NumericUpDown>(main, "aiPitch").Value = 7;
                using (var lease = (IDisposable)Call(main, "AcquireAiPreview"))
                using (var cancellation = new CancellationTokenSource())
                {
                    Check(Get<int>(lease, "Pitch") == 7 && ReferenceEquals(Get<object>(lease, "Runtime"), Get<object>(main, "aiRuntime")),
                        "AI preview borrows the existing engine and pitch instead of starting a conflicting engine");
                    Check(Call(main, "AcquireAiPreview") == null, "Only one AI preview may reserve the engine");
                    Set(lease, "Prepared", true);
                    Set(test, "aiTestCancellation", cancellation);
                    Call(test, "SetRunning", false);
                    Check(!Get<Button>(main, "btnStart").Enabled && !Get<Button>(test, "start").Enabled && Get<Button>(test, "aiTestStop").Enabled,
                        "Preview locks both streaming start actions and keeps its own Stop action available");
                    Call(main, "btnStart_Click", main, EventArgs.Empty);
                    Check(Get<object>(main, "waveIn") == null, "A direct main start request cannot bypass the AI preview reservation");
                    CheckAudioPanels(main, false, "Main audio panels stay locked throughout independent AI testing");

                    var input = new WaveInEvent();
                    var buffer = new BufferedWaveProvider(new WaveFormat(44100, 1));
                    var entered = new TaskCompletionSource<bool>();
                    var converted = new TaskCompletionSource<byte[]>();
                    Func<byte[], CancellationToken, Task<byte[]>> convert = (pcm, token) => { entered.TrySetResult(true); return converted.Task; };
                    Call(test, "AttachAiTestSession", input, buffer, convert);
                    var raw = new byte[26460];
                    raw[0] = 1;
                    Call(test, "AiTestDataAvailable", input, new WaveInEventArgs(raw, raw.Length));
                    Check(entered.Task.Wait(3000) && buffer.BufferedBytes == 0, "AI preview sends microphone data to inference without leaking raw audio to headphones");
                    var pcm = new byte[26460];
                    pcm[0] = 0xd0; pcm[1] = 7;
                    converted.SetResult(pcm);
                    Check(SpinWait.SpinUntil(() => buffer.BufferedBytes == pcm.Length, 3000), "Converted AI preview PCM reaches the dedicated headphone buffer");
                    var heard = new byte[pcm.Length];
                    buffer.Read(heard, 0, heard.Length);
                    Check(BitConverter.ToInt16(heard, 0) == 2000 && Get<object>(main, "waveProvider") == null && mainStartRequests == 0,
                        "AI test output is independent of the main stream and never invokes automatic main start");
                    var monitor = new VolumeWaveProvider16(buffer);
                    Set(test, "aiTestMonitorVolume", monitor);
                    Get<TrackBar>(test, "aiTestVolume").Value = 43;
                    Check(Math.Abs(monitor.Volume - .43f) < .001f && Get<TrackBar>(test, "volume").Value == 30,
                        "AI listening volume changes only the independent test output");
                    Get<ComboBox>(main, "languageSelector").SelectedIndex = 1;
                    Check(Get<Label>(test, "aiTestVolumeLabel").Text.StartsWith("Listening volume: 43%") && Get<Button>(test, "aiTestStop").Enabled,
                        "Language changes retain AI test settings and Stop availability");
                    Get<ComboBox>(main, "languageSelector").SelectedIndex = 0;
                    Call(test, "StopAiTest");
                    Check(cancellation.IsCancellationRequested && !Get<Button>(test, "aiTestStop").Enabled,
                        "Stop cancels the AI test and prevents duplicate cancellation");
                    Pump((Task)Call(test, "ReleaseAiTestAudioAsync"));
                    Call(test, "AiTestDataAvailable", input, new WaveInEventArgs(raw, raw.Length));
                    Check(Get<object>(test, "aiTestCapture") == null && Get<object>(test, "aiTestSession") == null && buffer.BufferedBytes == 0,
                        "AI test cleanup detaches capture and conversion and rejects late microphone data");
                    Set(test, "aiTestCancellation", null);
                }
                Call(test, "SetRunning", false);
                Check(!Get<bool>(main, "aiPreviewActive") && Get<Button>(main, "btnStart").Enabled && Get<Button>(test, "aiTestStart").Enabled
                    && Get<bool>(main, "aiEnabled") && Get<NumericUpDown>(main, "aiPitch").Value == 7,
                    "Finishing a prepared AI test restores Start and preserves the main AI choice and pitch");
                CheckAudioPanels(main, false, "Finishing AI preview leaves main streaming stopped and its panels locked");

                using (var cancellation = new CancellationTokenSource())
                using (var lease = (IDisposable)Call(main, "AcquireAiPreview"))
                {
                    Set(test, "aiTestCancellation", cancellation);
                    var old = new WaveInEvent();
                    Func<byte[], CancellationToken, Task<byte[]>> convert = (pcm, token) => Task.FromResult(pcm);
                    Call(test, "AttachAiTestSession", old, new BufferedWaveProvider(new WaveFormat(44100, 1)), convert);
                    Call(test, "AiTestSessionStopped", old, new IOException("Old preview"));
                    Pump((Task)Call(test, "ReleaseAiTestAudioAsync"));
                    var current = new WaveInEvent();
                    Call(test, "AttachAiTestSession", current, new BufferedWaveProvider(new WaveFormat(44100, 1)), convert);
                    Application.DoEvents();
                    Check(!cancellation.IsCancellationRequested, "Late device-stop notifications cannot cancel a replacement AI preview");
                    Call(test, "AiTestSessionStopped", current, new IOException("Preview microphone disconnected"));
                    Application.DoEvents();
                    Check(cancellation.IsCancellationRequested && Get<Exception>(test, "aiTestError").Message.Contains("disconnected"),
                        "AI preview device failure cancels testing and retains the error for display");
                    Call(test, "AiTestSessionStopped", current, null);
                    Application.DoEvents();
                    Check(Get<Exception>(test, "aiTestError").Message.Contains("disconnected"),
                        "A subsequent normal stop notification cannot hide the original AI preview error");
                    Pump((Task)Call(test, "ReleaseAiTestAudioAsync"));
                    Set(test, "aiTestCancellation", null);
                }
                Check(!Get<bool>(main, "aiEnabled") && !Get<bool>(main, "aiPreviewActive"), "Unfinished AI preparation releases the engine reservation and clears stale readiness");
                using (var cancellation = new CancellationTokenSource())
                {
                    Set(test, "aiTestCancellation", cancellation);
                    test.Dispose();
                    Check(cancellation.IsCancellationRequested, "Closing the test window cancels AI preparation or listening");
                    Set(test, "aiTestCancellation", null);
                }
            }
        }
    }

    private static WaveInEvent ConfigureSession(Form1 form)
    {
        Call(form, "StopAudio");
        var input = new WaveInEvent();
        var output = new WaveOutEvent();
        Set(form, "waveIn", input);
        Set(form, "waveOut", output);
        Set(form, "waveProvider", new BufferedWaveProvider(new WaveFormat(44100, 1)) { DiscardOnBufferOverflow = true });
        Call(form, "UpdateFilters");
        Call(form, "AttachAudioSessionHandlers", input, output);
        Call(form, "SetAudioRunning", true);
        return input;
    }

    private static Action<object, WaveInEventArgs> Processor(Form1 form)
    {
        return (Action<object, WaveInEventArgs>)Delegate.CreateDelegate(typeof(Action<object, WaveInEventArgs>), form,
            typeof(Form1).GetMethod("WaveIn_DataAvailable", Hidden));
    }

    private static void EchoAndConcurrency()
    {
        using (var form = NewForm())
        {
            var input = ConfigureSession(form);
            var process = Processor(form);
            var output = Get<BufferedWaveProvider>(form, "waveProvider");
            SetEcho(form, 10);
            process(input, new WaveInEventArgs(new byte[120000], 120000));
            output.ClearBuffer();
            SetEcho(form, 1);
            var bytes = new byte[120000];
            bytes[0] = 0x10; bytes[1] = 0x27;
            process(input, new WaveInEventArgs(bytes, bytes.Length));
            int firstEcho = -1;
            for (int i = 1; i < bytes.Length / 2; i++)
                if (Math.Abs((int)BitConverter.ToInt16(bytes, i * 2)) > 100) { firstEcho = i; break; }
            Check(firstEcho == 5292, "Reducing echo from 1200 ms to 120 ms produces the first echo at 120 ms");
            Check(Get<Queue<float>>(form, "echoBufferQueue").Count == 5292, "Echo buffer shrinks to the selected delay");
            output.ClearBuffer();

            int stop = 0;
            int callbacks = 0;
            Exception workerError = null;
            var worker = new Thread(() =>
            {
                try
                {
                    var data = new byte[17640];
                    var args = new WaveInEventArgs(data, data.Length);
                    while (Volatile.Read(ref stop) == 0)
                    {
                        process(input, args);
                        output.ClearBuffer();
                        Interlocked.Increment(ref callbacks);
                    }
                }
                catch (Exception ex) { workerError = ex; }
            });
            worker.Start();
            for (int i = 0; i < 1000; i++)
            {
                SetEcho(form, i % 2 == 0 ? 0 : 10);
                if (i % 25 == 0) Call(form, "btnReset_Click", form, EventArgs.Empty);
            }
            Volatile.Write(ref stop, 1);
            Check(worker.Join(5000), "Concurrent effect changes finish without a deadlock");
            Application.DoEvents();
            Check(workerError == null && callbacks > 0 && ReferenceEquals(Get<WaveInEvent>(form, "waveIn"), input),
                "1000 echo changes and resets keep the audio session alive without callback errors");

            SetEcho(form, 0);
            for (int i = 0; i < 15; i++)
            {
                var item = new SoundItem { Name = "Test sound", Path = audioPath };
                Check((bool)Call(form, "TryCommitSoundList", new List<SoundItem> { item }), "Prepare library for concurrent removal " + (i + 1));
                Get<ListBox>(form, "listBoxSounds").SelectedIndex = 0;
                Pump((Task)Call(form, "LoadCustomSoundAsync", audioPath));
                object playback = Get<object>(form, "playingSound");
                Check(playback != null, "Sound starts before concurrent removal " + (i + 1));
                int done = 0;
                workerError = null;
                var soundWorker = new Thread(() =>
                {
                    try
                    {
                        var data = new byte[17640];
                        while (Volatile.Read(ref done) == 0)
                        {
                            process(input, new WaveInEventArgs(data, data.Length));
                            output.ClearBuffer();
                        }
                    }
                    catch (Exception ex) { workerError = ex; }
                });
                soundWorker.Start();
                Call(form, "btnDeleteSound_Click", form, EventArgs.Empty);
                Volatile.Write(ref done, 1);
                Check(soundWorker.Join(5000), "Concurrent removal finishes without a deadlock " + (i + 1));
                Application.DoEvents();
                Check(workerError == null && Get<object>(form, "playingSound") == null && ReferenceEquals(Get<WaveInEvent>(form, "waveIn"), input),
                    "Removing a playing sound preserves microphone streaming " + (i + 1));
                Check(Property<Task>(playback, "Completion").Wait(5000), "Removed sound releases its decoder " + (i + 1));
            }
        }
    }

    private static void StreamingAndCancellation()
    {
        var playbackType = typeof(Form1).Assembly.GetType("ses_uyg.SoundPlayback", true);
        var timer = Stopwatch.StartNew();
        var standalone = Activator.CreateInstance(playbackType, Hidden, null, new object[] { audioPath }, null);
        WaitReady(standalone);
        var full = SpinWait.SpinUntil(() => Get<int>(standalone, "count") == 88200, 5000);
        Check(full && Get<float[]>(standalone, "samples").Length == 88200, "A ten-minute file uses a fixed two-second sample buffer");
        Check(!Property<Task>(standalone, "Completion").IsCompleted, "Decoder waits at the buffer limit instead of loading the whole file");
        ((IDisposable)standalone).Dispose();
        Check(Property<Task>(standalone, "Completion").Wait(5000), "Cancellation wakes a producer waiting on a full buffer");
        using (new FileStream(audioPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Check(true, "Cancelled playback releases the audio file handle");

        using (var form = NewForm())
        {
            ConfigureSession(form);
            bool uiTick = false;
            form.BeginInvoke(new Action(() => uiTick = true));
            var load = (Task)Call(form, "LoadCustomSoundAsync", audioPath);
            Application.DoEvents();
            Check(uiTick, "UI messages are processed while a large sound is prepared");
            Pump(load);
            Check(Get<object>(form, "playingSound") != null, "Large sound is published after background preparation");

            var first = (Task)Call(form, "LoadCustomSoundAsync", audioPath);
            var firstPlayback = Get<object>(form, "pendingSound") ?? Get<object>(form, "playingSound");
            var second = (Task)Call(form, "LoadCustomSoundAsync", audioPath);
            var secondPlayback = Get<object>(form, "pendingSound") ?? Get<object>(form, "playingSound");
            Pump(Task.WhenAll(first, second));
            Check(ReferenceEquals(Get<object>(form, "playingSound"), secondPlayback), "Only the latest sound request is published");
            Check(Property<Task>(firstPlayback, "Completion").Wait(5000), "Superseded sound decoder is cancelled");

            var pending = (Task)Call(form, "LoadCustomSoundAsync", audioPath);
            var pendingPlayback = Get<object>(form, "pendingSound") ?? Get<object>(form, "playingSound");
            Call(form, "btnStop_Click", form, EventArgs.Empty);
            Pump(pending);
            Check(Get<object>(form, "playingSound") == null && Get<object>(form, "pendingSound") == null,
                "Stopping during preparation prevents late playback");
            Check(Property<Task>(pendingPlayback, "Completion").Wait(5000), "Stop cancels pending file I/O");

            ConfigureSession(form);
            Pump((Task)Call(form, "LoadCustomSoundAsync", Path.Combine(directory, "missing.wav")));
            Check(Get<object>(form, "playingSound") == null && Get<Label>(form, "soundStatus").Text.Contains("hata"),
                "Failed background loads report an error and leave no active sound");
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 1;
            Check(Get<Label>(form, "soundStatus").Text.StartsWith("Could not load the sound:"), "Background-load errors change to English with the UI");
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 0;

            var closing = (Task)Call(form, "LoadCustomSoundAsync", audioPath);
            var closingPlayback = Get<object>(form, "pendingSound") ?? Get<object>(form, "playingSound");
            form.Dispose();
            Pump(closing);
            Check(Property<Task>(closingPlayback, "Completion").Wait(5000), "Closing the form cancels background decoding");
        }

        string small = Path.Combine(directory, "short.wav");
        WriteWave(small, 1);
        var shortPlayback = Activator.CreateInstance(playbackType, Hidden, null, new object[] { small }, null);
        WaitReady(shortPlayback);
        int nonzero = 0;
        var timeout = Stopwatch.StartNew();
        while (!Property<bool>(shortPlayback, "Finished") && timeout.ElapsedMilliseconds < 5000)
        {
            var data = new float[4410];
            Call(shortPlayback, "MixInto", data, data.Length);
            nonzero += data.Count(sample => sample != 0f);
            Thread.Sleep(1);
        }
        Check(Property<bool>(shortPlayback, "Finished") && Property<Exception>(shortPlayback, "Error") == null && nonzero == 44100,
            "Streaming consumes every sample and reaches EOF without truncating the file");
        ((IDisposable)shortPlayback).Dispose();
    }

    private static void SessionRecovery()
    {
        using (var form = NewForm())
        {
            var old = ConfigureSession(form);
            Raise(old, "RecordingStopped", new StoppedEventArgs(new IOException("Disconnected microphone")));
            Application.DoEvents();
            Check(Get<WaveInEvent>(form, "waveIn") == null && Get<WaveOutEvent>(form, "waveOut") == null,
                "RecordingStopped releases both audio devices");
            Check(Get<Button>(form, "btnStart").Enabled && !Get<Button>(form, "btnStop").Enabled && Get<Button>(form, "refreshDevices").Enabled,
                "Recording failure restores start and device-selection controls");
            Check(Get<Label>(form, "routingStatus").Text.Contains("Disconnected microphone"), "Recording failure is reported to the user");
            CheckAudioPanels(form, false, "Recording failure relocks audio panels");

            ConfigureSession(form);
            var output = Get<WaveOutEvent>(form, "waveOut");
            Raise(output, "PlaybackStopped", new StoppedEventArgs(new IOException("Disconnected cable")));
            Application.DoEvents();
            Check(Get<WaveInEvent>(form, "waveIn") == null && Get<Label>(form, "routingStatus").Text.Contains("Disconnected cable"),
                "PlaybackStopped also stops capture and reports the error");
            CheckAudioPanels(form, false, "Output failure relocks audio panels");

            old = ConfigureSession(form);
            Raise(old, "RecordingStopped", new StoppedEventArgs(new IOException("Old session")));
            var current = ConfigureSession(form);
            Application.DoEvents();
            Check(ReferenceEquals(Get<WaveInEvent>(form, "waveIn"), current), "A delayed stop event cannot stop the replacement session");
            Processor(form)(old, new WaveInEventArgs(new byte[8820], 8820));
            Check(Get<BufferedWaveProvider>(form, "waveProvider").BufferedBytes == 0, "Late data from a stopped session is discarded");

            Set(form, "eqBassFilter", null); // Inject an unexpected DSP failure.
            Processor(form)(current, new WaveInEventArgs(new byte[8820], 8820));
            Application.DoEvents();
            Check(Get<WaveInEvent>(form, "waveIn") == null && Get<Button>(form, "btnStart").Enabled,
                "A callback exception is handled without leaving the UI in a running state");
        }
    }

    private static void LibraryTransactions()
    {
        using (var form = NewForm())
        {
            string config = Get<string>(form, "configFilePath");
            var before = new List<SoundItem> { new SoundItem { Name = "Before", Path = audioPath } };
            Check((bool)Call(form, "TryCommitSoundList", before), "Initial library save succeeds");
            string original = File.ReadAllText(config);
            var after = new List<SoundItem>(before) { new SoundItem { Name = "After", Path = "after.wav" } };
            using (new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.None))
                Check(!(bool)Call(form, "TryCommitSoundList", after), "A locked library returns a save failure");
            Check(File.ReadAllText(config) == original && Get<List<SoundItem>>(form, "soundList").Count == 1,
                "Failed save preserves both the existing file and the displayed library");
            Check(Get<Label>(form, "libraryStatus").Text.Contains("kaydedilemedi"), "Save failure is visible in Turkish");
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 1;
            Check(Get<Label>(form, "libraryStatus").Text.StartsWith("The library could not be saved"), "Existing save failure changes to English");
            Get<ComboBox>(form, "languageSelector").SelectedIndex = 0;
            Check(Directory.GetFiles(directory, ".soundboard-*.tmp").Length == 0, "Failed saves clean up their temporary file");
            Check((bool)Call(form, "TryCommitSoundList", after), "Saving can be retried after the file is unlocked");
            Call(form, "LoadSoundListFromDisk");
            Check(Get<List<SoundItem>>(form, "soundList").Count == 2, "Successful replacement can be loaded again");

            File.WriteAllText(config, "<invalid-library>");
            Call(form, "LoadSoundListFromDisk");
            ConfigureSession(form);
            Check(Get<bool>(form, "libraryLoadFailed") && !Get<Button>(form, "btnAddSound").Enabled,
                "Starting audio preserves the editing lock on unreadable libraries");
            Check(!(bool)Call(form, "TryCommitSoundList", before) && File.ReadAllText(config) == "<invalid-library>",
                "An unreadable library cannot be silently overwritten");
        }
    }

    private static void SetEcho(Form1 form, int value)
    {
        Get<TrackBar>(form, "trackBarEcho").Value = value;
        Call(form, "trackBarEcho_Scroll", form, EventArgs.Empty);
    }

    private static void WaitReady(object playback)
    {
        var task = Property<Task>(playback, "Ready");
        Check(task.Wait(5000), "Background decoder becomes ready");
        task.GetAwaiter().GetResult();
    }

    private static void Pump(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.ElapsedMilliseconds < 10000) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new Exception("Timed out waiting for asynchronous UI work");
        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    private static void Raise(object sender, string eventName, StoppedEventArgs args)
    {
        var handler = (Delegate)sender.GetType().GetField(eventName, Hidden).GetValue(sender);
        if (handler == null) throw new Exception("No handler attached to " + eventName);
        handler.DynamicInvoke(sender, args);
    }

    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Hidden).GetValue(target); }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Hidden).SetValue(target, value); }
    private static T Property<T>(object target, string name) { return (T)target.GetType().GetProperty(name, Hidden).GetValue(target); }
    private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Hidden).Invoke(target, args); }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        checks++;
        Console.WriteLine("PASS: " + description);
    }

    private static void WriteWave(string path, int seconds)
    {
        using (var writer = new WaveFileWriter(path, new WaveFormat(44100, 1)))
        {
            var data = new byte[88200];
            for (int i = 0; i < data.Length; i += 2) { data[i] = 0x10; data[i + 1] = 0x27; }
            for (int i = 0; i < seconds; i++) writer.Write(data, 0, data.Length);
        }
    }
}
