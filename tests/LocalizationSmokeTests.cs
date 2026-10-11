using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Uses transparent test windows for rendering; never starts microphone capture.
// Compile with csc /r:System.Windows.Forms.dll /r:System.Drawing.dll.
internal static class LocalizationSmokeTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static Type uiText;
    private static int checks;
    private static bool headless;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            headless = args.Length > 2 && args[2] == "--headless";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            uiText = assembly.GetType("ses_uyg.UiText", true);
            using (var main = NewMain(assembly.GetType("ses_uyg.Form1", true)))
            {
                var languages = Field<ComboBox>(main, "languageSelector");
                Check(languages.Items.Count == 2 && languages.SelectedIndex == 0, "Turkish is the default; both languages are available");
                Check(main.Text == "Ses Stüdyosu", "Turkish main title");
                CheckSettings(main, assembly, languages, args[1]);
                Check(main.Controls.Find("exitApplicationButton", true).Single().Text == "Uygulamayı kapat",
                    "The full-exit button is localized in Turkish");
                Check(main.Controls.Find("aiVoiceSection", true).Length == 1, "AI voice has a separate section on the main page");
                Check(Field<NumericUpDown>(main, "aiPitch").Value == 12, "Female voice defaults to +12 semitones");
                var voiceSelector = (ComboBox)main.Controls.Find("aiVoiceSelector", true).Single();
                Check(voiceSelector.Items.Count == 9 && !voiceSelector.Enabled, "Nine voice choices obey the stopped-stream lock");
                Check(!main.Controls.Find("aiVoiceRemove", true).Single().Enabled && main.Controls.Find("aiVoiceStorage", true).Single().Text.Contains("Toplam:"),
                    "Main model management shows total space and obeys stopped-stream lock");
                Check(Field<Button>(main, "aiEnableButton").Text == "AI sesi etkinleştir", "AI voice action is localized");
                Check(!Field<Button>(main, "aiEnableButton").Enabled && !Field<TrackBar>(main, "trackBarPitch").Enabled
                    && !Field<TextBox>(main, "txtSearch").Enabled && Field<Button>(main, "testAudioButton").Enabled,
                    "Audio controls start locked while Test audio is accessible");
                SavePreview(main, Path.Combine(args[1], "main-locked-tr.png"));
                Check(main.Controls.Find("personalizeButton", true).Length == 1 && main.Controls.Find("personalizeButton", true)[0].Enabled,
                    "Personalization is available before main streaming starts");
                using (var editor = (Form)Activator.CreateInstance(assembly.GetType("ses_uyg.PersonalizationForm", true), true))
                {
                    main.GetType().GetField("personalizationForm", Instance).SetValue(main, editor);
                    editor.GetType().GetMethod("SetPreset", Instance).Invoke(editor, new object[] { Color.LightBlue, Color.LightPink });
                    SavePreview(editor, Path.Combine(args[1], "appearance-tr.png"));
                    languages.SelectedIndex = 1;
                    Check(editor.Text == "Personalize your appearance" && Field<ComboBox>(editor, "mode").Text == "Gradient",
                        "An open personalization window changes language and retains its selected background type");
                    SavePreview(editor, Path.Combine(args[1], "appearance-en.png"));
                    editor.Size = editor.MinimumSize;
                    SavePreview(editor, Path.Combine(args[1], "appearance-small-en.png"));
                    languages.SelectedIndex = 0;
                }
                languages.SelectedIndex = 1;
                Check(Field<Label>(main, "audioLockedHint").Text.StartsWith("Start streaming to use"), "Locked-panel explanation changes to English");
                SavePreview(main, Path.Combine(args[1], "main-locked-en.png"));
                languages.SelectedIndex = 0;
                CheckAiSetup(main, assembly, languages, args[1]);
                var testType = assembly.GetType("ses_uyg.AudioTestForm", true);
                using (var test = NewTest(testType, main))
                {
                    main.GetType().GetField("audioTestForm", Instance).SetValue(main, test);
                    Check(test.Controls.Find("aiTestSection", true).Length == 1 && Field<Button>(test, "aiTestStart").Enabled,
                        "Test window has an independent AI panel available while main streaming is stopped");
                    var testVoices = (ComboBox)test.Controls.Find("aiVoiceSelector", true).Single();
                    Check(testVoices.Items.Count == 9 && testVoices.Enabled, "Test screen can select voices while main streaming is stopped");
                    Check(test.Controls.Find("aiVoiceRemove", true).Single().Text == "Seçili modeli sil"
                        && test.Controls.Find("aiVoiceStorage", true).Single().Text.Contains("Ortak AI motoru"), "Test model management is present in Turkish");
                    var testPicker = testVoices.Parent;
                    testPicker.GetType().GetMethod("SetAvailable", Instance).Invoke(testPicker, new object[] { false });
                    testPicker.GetType().GetMethod("RefreshStatus", Instance).Invoke(testPicker, null);
                    Check(!test.Controls.Find("aiVoiceRemove", true).Single().Enabled && !test.Controls.Find("aiVoiceDownload", true).Single().Enabled,
                        "Refreshing storage never unlocks model actions during an operation");
                    testPicker.GetType().GetMethod("SetAvailable", Instance).Invoke(testPicker, new object[] { true });
                    SavePreview(test, Path.Combine(args[1], "test-ai-idle-tr.png"));
                    languages.SelectedIndex = 1;
                    Check(Field<Button>(test, "aiTestStart").Text == "Start AI test", "Independent AI test is localized in English");
                    Check(test.Controls.Find("aiVoiceRemove", true).Single().Text == "Delete selected model"
                        && test.Controls.Find("aiVoiceStorage", true).Single().Text.Contains("Total:"), "Model management follows English language selection");
                    SavePreview(test, Path.Combine(args[1], "test-ai-idle-en.png"));
                    test.Size = test.MinimumSize;
                    SavePreview(test, Path.Combine(args[1], "test-ai-small-en.png"));
                    for (int language = 0; language < 2; language++)
                    {
                        languages.SelectedIndex = language;
                        for (int voice = 0; voice < testVoices.Items.Count; voice++)
                        {
                            testVoices.SelectedIndex = voice;
                            test.PerformLayout();
                            Application.DoEvents();
                            var picker = testVoices.Parent;
                            Check(picker.Controls.Cast<Control>().All(control => control.Right <= picker.ClientSize.Width),
                                "Voice picker fits the narrow test window: language " + language + ", voice " + voice);
                        }
                    }
                    testVoices.SelectedIndex = 0;
                    languages.SelectedIndex = 1;
                    test.ClientSize = new Size(1000, 860);
                    SavePreview(test, Path.Combine(args[1], "test-ai-large-en.png"));
                    test.ClientSize = new Size(700, 820);
                    languages.SelectedIndex = 0;
                    var source = Field<ComboBox>(test, "source");
                    var destination = Field<ComboBox>(test, "destination");
                    source.Items.Clear();
                    destination.Items.Clear();
                    source.Items.Add("CABLE Output (Test device)");
                    destination.Items.Add("Headphones (Test device)");
                    source.SelectedIndex = destination.SelectedIndex = 0;
                    var volume = Field<TrackBar>(test, "volume");
                    volume.Value = 67;
                    var microphone = Field<ComboBox>(main, "microphoneSelector");
                    microphone.Items.Add("Microphone (Test device)");
                    microphone.SelectedIndex = microphone.Items.Count - 1;
                    object microphoneSelection = microphone.SelectedItem;

                    var soundType = assembly.GetType("ses_uyg.SoundItem", true);
                    var sound = Activator.CreateInstance(soundType);
                    soundType.GetProperty("Name").SetValue(sound, "My test sound");
                    soundType.GetProperty("Path").SetValue(sound, @"C:\Sounds\My test sound.wav");
                    var sounds = Field<IList>(main, "soundList");
                    sounds.Clear();
                    sounds.Add(sound);
                    var search = Field<TextBox>(main, "txtSearch");
                    search.Text = "My test";
                    var soundList = Field<ListBox>(main, "listBoxSounds");
                    soundList.SelectedIndex = 0;
                    var pitch = Field<TrackBar>(main, "trackBarPitch");
                    pitch.Value = 17;
                    main.GetType().GetMethod("trackBarPitch_Scroll", Instance).Invoke(main, new object[] { pitch, EventArgs.Empty });

                    // The language action must preserve active-session objects and UI state.
                    var waveInField = main.GetType().GetField("waveIn", Instance);
                    var captureField = testType.GetField("capture", Instance);
                    using (var mainCapture = (IDisposable)Activator.CreateInstance(waveInField.FieldType))
                    using (var testCapture = (IDisposable)Activator.CreateInstance(captureField.FieldType))
                    {
                        waveInField.SetValue(main, mainCapture);
                        main.GetType().GetMethod("SetAudioRunning", Instance).Invoke(main, new object[] { true });
                        captureField.SetValue(test, testCapture);
                        Field<Button>(test, "start").Enabled = false;
                        Field<Button>(test, "stop").Enabled = true;
                        Set(Field<Label>(test, "status"), "TestRunning", source.SelectedItem, destination.SelectedItem);
                        Set(Field<Label>(main, "routingStatus"), "Installed", 123);
                        languages.SelectedIndex = 1;
                        Check(main.Text == "Audio Studio" && test.Text == "Test your outgoing audio", "Existing windows change to English together");
                        Check(Field<Button>(main, "btnStart").Text == "Start streaming", "Main button changes to English");
                        Check(main.Controls.Find("exitApplicationButton", true).Single().Text == "Exit application",
                            "The full-exit button changes to English");
                        Check(Field<Button>(test, "start").Text == "Start test", "Test button changes to English");
                        Check(Field<Label>(test, "volumeLabel").Text == "Listening volume: 67% (this test only)", "Test volume text keeps its current value");
                        Check(Field<Label>(test, "status").Text.StartsWith("Test running: CABLE Output (Test device) → Headphones (Test device)"), "Running status changes language and retains device names");
                        Check(Field<Label>(main, "routingStatus").Text.StartsWith("The installer closed (code: 123)"), "Existing maintenance status changes language and retains exit code");
                        Check(Field<Label>(main, "lblPitch").Text == "Pitch: HIGH (Chipmunk x1.7)", "Dynamic effect label changes language");
                        Check(pitch.Value == 17 && volume.Value == 67, "Effect and test volume settings are preserved");
                        Check(ReferenceEquals(waveInField.GetValue(main), mainCapture) && ReferenceEquals(captureField.GetValue(test), testCapture), "Language change preserves both audio sessions");
                        Check(!Field<Button>(test, "start").Enabled && Field<Button>(test, "stop").Enabled, "Running test button state is preserved");
                        Check(!Field<Button>(test, "aiTestStart").Enabled, "Independent AI test is blocked during main streaming");
                        Check(ReferenceEquals(microphone.SelectedItem, microphoneSelection) && source.SelectedIndex == 0 && destination.SelectedIndex == 0, "Device selections are preserved");
                        Check(search.Text == "My test" && ReferenceEquals(soundList.SelectedItem, sound), "Search and selected sound are preserved");
                        Check(Field<Label>(main, "soundCount").Text == "1 / 1 sounds", "Sound count changes language");
                        Check(Field<Label>(main, "selectedSoundPath").Text == @"C:\Sounds\My test sound.wav", "File paths are not translated");
                        SavePreview(main, Path.Combine(args[1], "main-en.png"));
                        SavePreview(test, Path.Combine(args[1], "test-en.png"));
                        main.ClientSize = new Size(924, 611);
                        SavePreview(main, Path.Combine(args[1], "main-en-small.png"));
                        main.ClientSize = new Size(1100, 860);

                        languages.SelectedIndex = 0;
                        Check(main.Text == "Ses Stüdyosu" && test.Text == "Karşıya giden sesi test et", "Existing windows return to Turkish");
                        Check(Field<Label>(test, "volumeLabel").Text == "Dinleme sesi: %67 (yalnızca bu test)", "Turkish volume text keeps its current value");
                        Check(Field<Label>(test, "status").Text.StartsWith("Test açık:"), "Running status returns to Turkish");
                        Check(Field<Label>(main, "soundCount").Text == "1 / 1 ses", "Sound count returns to Turkish");
                        SavePreview(main, Path.Combine(args[1], "main-tr.png"));
                        SavePreview(test, Path.Combine(args[1], "test-tr.png"));
                        waveInField.SetValue(main, null);
                        main.GetType().GetMethod("SetAudioRunning", Instance).Invoke(main, new object[] { false });
                        captureField.SetValue(test, null);
                    }

                    testType.GetMethod("StopTest", Instance).Invoke(test, null);
                    languages.SelectedIndex = 1;
                    Check(Field<Label>(test, "status").Text.StartsWith("Test stopped."), "Stopped test status changes language");
                    var error = uiText.GetMethod("Error", Static).Invoke(null, new object[] { "DevicesChanged", new object[0] });
                    Set(Field<Label>(test, "status"), "TestStartError", error);
                    languages.SelectedIndex = 0;
                    Check(Field<Label>(test, "status").Text == "Test başlatılamadı: Ses cihazları değişti. Cihazları yenileyip tekrar seçin.", "Existing application error is translated including its details");
                    languages.SelectedIndex = 1;
                    search.Text = "no matching sound";
                    Check(Field<Label>(main, "soundCount").Text == "No sounds match your search.", "New dynamic messages use the selected language");
                    languages.SelectedIndex = 0;
                    Check(Field<Label>(main, "soundCount").Text == "Aramanızla eşleşen ses yok.", "Existing empty search message changes language");
                }

                languages.SelectedIndex = 1;
                using (var reopened = NewTest(testType, main))
                    Check(reopened.Text == "Test your outgoing audio" && Field<Button>(reopened, "start").Text == "Start test", "Reopened test window uses the current language");
                // Instantiate the main window in English too: no control lookup may depend on translated text.
                using (var reopenedMain = NewMain(main.GetType()))
                    Check(reopenedMain.Text == "Audio Studio", "Main window can initialize directly in English");
            }
            Console.WriteLine("PASS: " + checks + " localization integration checks; no microphone capture started.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static Form NewTest(Type type, Form main)
    {
        var lease = type.Assembly.GetType("ses_uyg.AiPreviewLease", true);
        var acquire = Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(lease), main, main.GetType().GetMethod("AcquireAiPreview", Instance));
        return (Form)Activator.CreateInstance(type, Instance, null, new object[] { new Func<bool>(() => false), acquire }, null);
    }

    private static Form NewMain(Type type)
    {
        return (Form)Activator.CreateInstance(type, Instance, null,
            new object[] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-settings-" + Guid.NewGuid().ToString("N")) }, null);
    }

    private static void CheckSettings(Form main, Assembly assembly, ComboBox languages, string previews)
    {
        Check(main.Controls.Find("settingsButton", true).Single().AccessibleName == "Ayarlar", "The corner gear has an accessible settings label");
        using (var settings = (Form)Activator.CreateInstance(assembly.GetType("ses_uyg.SettingsForm", true), Instance, null, new object[] { main }, null))
        {
            main.GetType().GetField("settingsForm", Instance).SetValue(main, settings);
            Check(Field<CheckBox>(settings, "closeToTray").Checked && !Field<CheckBox>(settings, "startInTray").Checked,
                "Settings exposes independent close and opening preferences");
            SavePreview(settings, Path.Combine(previews, "settings-tr.png"));
            languages.SelectedIndex = 1;
            Check(settings.Text == "Settings" && Field<CheckBox>(settings, "startWithWindows").Text.Contains("Windows starts"),
                "An open Settings window follows language changes");
            SavePreview(settings, Path.Combine(previews, "settings-en.png"));
            settings.Size = settings.MinimumSize;
            SavePreview(settings, Path.Combine(previews, "settings-small-en.png"));
            languages.SelectedIndex = 0;
        }
    }

    private static void CheckAiSetup(Form main, Assembly assembly, ComboBox languages, string previews)
    {
        var bar = Field<ProgressBar>(main, "aiInstallProgress");
        Check(main.Controls.Find("aiInstallProgress", true).Length == 1, "AI setup progress belongs to the main window");
        var stage = Enum.Parse(assembly.GetType("ses_uyg.AiInstallStage", true), "Download");
        var progress = Activator.CreateInstance(assembly.GetType("ses_uyg.AiInstallProgress", true), Instance, null,
            new object[] { stage, 1000000000L, 3308026202L }, null);
        var preparation = main.GetType().GetField("aiPreparation", Instance);
        var waveIn = main.GetType().GetField("waveIn", Instance);
        using (var capture = (IDisposable)Activator.CreateInstance(waveIn.FieldType))
        using (var cancellation = new CancellationTokenSource())
        {
            waveIn.SetValue(main, capture);
            main.GetType().GetMethod("SetAudioRunning", Instance).Invoke(main, new object[] { true });
            var voices = (ComboBox)main.Controls.Find("aiVoiceSelector", true).Single();
            voices.SelectedIndex = 3;
            Check(!Field<NumericUpDown>(main, "aiPitch").Enabled, "No F0 voices disable inapplicable pitch control");
            voices.SelectedIndex = 0;
            Check(Field<NumericUpDown>(main, "aiPitch").Enabled, "Pitch control returns for pitched voices");
            preparation.SetValue(main, cancellation);
            try
            {
                main.GetType().GetMethod("UpdateAiControls", Instance).Invoke(main, null);
                main.GetType().GetMethod("ShowAiInstallProgress", Instance).Invoke(main, new[] { progress });
                bar.Visible = true;
                Check(bar.Value == 30 && Field<Label>(main, "aiStatus").Text.StartsWith("AI motoru indiriliyor:"), "Download bytes and progress are shown in Turkish");
                Check(!Field<Button>(main, "btnStart").Enabled && !Field<Button>(main, "aiEnableButton").Enabled && !Field<Button>(main, "installCable").Enabled,
                    "Duplicate capture and setup cannot start while AI is being installed");
                Check(Field<Button>(main, "btnStop").Enabled && Field<Button>(main, "aiDisableButton").Enabled,
                    "Stop streaming and Cancel AI remain usable during installation");
                languages.SelectedIndex = 1;
                Check(Field<Label>(main, "aiStatus").Text.StartsWith("Downloading AI engine: 30%") && bar.Value == 30,
                    "Switching language keeps the current installation progress");
                SavePreview(main, Path.Combine(previews, "install-en.png"));
                languages.SelectedIndex = 0;
                SavePreview(main, Path.Combine(previews, "install-tr.png"));
                main.GetType().GetMethod("DisableAi", Instance).Invoke(main, null);
                Check(cancellation.IsCancellationRequested && !Field<Button>(main, "aiDisableButton").Enabled && !Field<Button>(main, "btnStart").Enabled,
                    "Cancel requests cancellation without allowing duplicate capture");
            }
            finally
            {
                preparation.SetValue(main, null);
                main.GetType().GetMethod("UpdateAiControls", Instance).Invoke(main, null);
                Check(!Field<Button>(main, "btnStart").Enabled && Field<Button>(main, "aiEnableButton").Enabled
                    && ReferenceEquals(waveIn.GetValue(main), capture), "AI cancellation preserves streaming and allows AI to be retried");
                waveIn.SetValue(main, null);
                main.GetType().GetMethod("SetAudioRunning", Instance).Invoke(main, new object[] { false });
            }
        }
        bar.Visible = false;
        main.GetType().GetMethod("UpdateAiControls", Instance).Invoke(main, null);
        Check(Field<Button>(main, "btnStart").Enabled && !Field<Button>(main, "aiEnableButton").Enabled, "Stopped audio keeps the AI panel locked after cleanup");
        Set(Field<Label>(main, "aiStatus"), "AiNotInstalled");
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, Instance).GetValue(target);
    }

    private static void Set(Control control, string key, params object[] values)
    {
        uiText.GetMethod("Set", Static).Invoke(null, new object[] { control, key, values });
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        checks++;
        Console.WriteLine("PASS: " + description);
    }

    private static void SavePreview(Form form, string path)
    {
        if (headless) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!form.Visible)
        {
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.Show();
        }
        Application.DoEvents();
        form.PerformLayout();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(path);
        }
    }
}
