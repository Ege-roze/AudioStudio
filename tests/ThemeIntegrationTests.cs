using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class ThemeIntegrationTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static Assembly app;
    private static object store;
    private static int checks;
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme-tests-" + Guid.NewGuid().ToString("N"));
            store = Create("AppearanceStore", root);
            SaveTheme(0);
            using (var main = (Form)Create("Form1", Path.Combine(root, "main")))
            {
                Show(main);
                var lease = app.GetType("ses_uyg.AiPreviewLease", true);
                var acquire = Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(lease), main, main.GetType().GetMethod("AcquireAiPreview", Instance));
                using (var test = (Form)Create("AudioTestForm", new Func<bool>(() => false), acquire))
                using (var settings = (Form)Create("SettingsForm", main))
                using (var about = (Form)Create("AboutForm"))
                {
                    Show(test); Show(settings); Show(about);
                    var captureField = main.GetType().GetField("waveIn", Instance);
                    object capture = Activator.CreateInstance(captureField.FieldType);
                    captureField.SetValue(main, capture);
                    main.GetType().GetMethod("SetAudioRunning", Instance).Invoke(main, new object[] { true });
                    IntPtr handle = main.Handle;
                    var start = Field<Button>(main, "btnStart");
                    Color glassButton = start.BackColor;
                    Render(main, args[1], "theme-glass-main.png");
                    SaveTheme(1);
                    Check(ReferenceEquals(captureField.GetValue(main), capture) && main.Handle == handle && Field<Button>(main, "btnStop").Enabled,
                        "Changing theme keeps the existing audio session, window handle and stream controls");
                    Check(start.BackColor != glassButton, "Existing action buttons receive the Classic palette");
                    foreach (var entry in new[] { Tuple.Create(main, "main"), Tuple.Create(test, "test"), Tuple.Create(settings, "settings"), Tuple.Create(about, "about") })
                        Render(entry.Item1, args[1], "theme-classic-" + entry.Item2 + ".png");
                    main.AutoScrollPosition = new Point(0, 520);
                    Render(main, args[1], "theme-classic-main-scrolled.png");
                    main.AutoScrollPosition = Point.Empty;
                    using (var editor = (Form)Create("PersonalizationForm"))
                    {
                        Show(editor);
                        var selector = Field<ComboBox>(editor, "theme");
                        Check(selector.SelectedIndex == 1 && !Field<Button>(editor, "save").Visible, "A newly opened editor loads Classic without an unsaved change");
                        Render(editor, args[1], "theme-classic-personalization.png");
                        selector.SelectedIndex = 0;
                        Check(Field<Button>(editor, "save").Visible, "Changing the pack exposes Save");
                        Render(editor, args[1], "theme-classic-glass-preview.png");
                        selector.SelectedIndex = 1;
                        Check(!Field<Button>(editor, "save").Visible, "Returning to the saved pack hides Save");
                        editor.Size = editor.MinimumSize;
                        Render(editor, args[1], "theme-classic-personalization-small.png");
                    }
                    using (var later = (Form)Create("AudioTestForm", new Func<bool>(() => false), acquire))
                        Check(Field<Button>(later, "aiTestStart").BackColor == Field<Button>(test, "aiTestStart").BackColor,
                            "Windows opened after saving use the same Classic palette as existing windows");
                    SaveTheme(0);
                    Check(start.BackColor == glassButton && ReferenceEquals(captureField.GetValue(main), capture), "Returning to Glass restores the original colors without restarting audio");
                    Render(main, args[1], "theme-glass-restored.png");
                    main.GetType().GetMethod("StopAudio", Instance).Invoke(main, null);
                    test.Size = test.MinimumSize;
                    SaveTheme(1);
                    Render(test, args[1], "theme-classic-test-small.png");
                    Check(test.Controls.Find("aiVoiceSelector", true).Single().Width > 250, "The Classic voice selector remains usable at minimum window size");
                    using (var editor = (Form)Create("PersonalizationForm"))
                    {
                        Show(editor);
                        Field<ComboBox>(editor, "theme").SelectedIndex = 0;
                        Field<ComboBox>(editor, "theme").SelectedIndex = 1;
                        SaveTheme(0);
                    }
                }
            }
            Console.WriteLine("PASS: " + checks + " theme integration checks; synthetic session only, settings isolated.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static object Create(string name, params object[] args)
    { return Activator.CreateInstance(app.GetType("ses_uyg." + name, true), Instance, null, args, null); }
    private static void SaveTheme(int value)
    {
        object options = Create("BackgroundOptions");
        var property = options.GetType().GetProperty("Theme");
        property.SetValue(options, Enum.ToObject(property.PropertyType, value), null);
        app.GetType("ses_uyg.BackgroundAppearance").GetMethod("Save", Static).Invoke(null, new object[] { options, null, store });
        Application.DoEvents();
    }
    private static T Field<T>(object target, string field) { return (T)target.GetType().GetField(field, Instance).GetValue(target); }
    private static void Show(Form window) { window.Opacity = 0; window.ShowInTaskbar = false; window.Show(); Application.DoEvents(); }
    private static void Render(Form window, string directory, string name)
    {
        Directory.CreateDirectory(directory);
        window.PerformLayout(); Application.DoEvents();
        using (var bitmap = new Bitmap(window.Width, window.Height))
        { window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, name)); }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
}
