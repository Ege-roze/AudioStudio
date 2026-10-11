using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ses_uyg;

internal static class AppearanceRegressionTests
{
    private static int checks;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main()
    {
        try
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appearance-tests-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "settings");
            var store = new AppearanceStore(folder);
            Bitmap loaded;
            Check(store.Load(out loaded).Kind == BackgroundKind.Default && loaded == null && !Directory.Exists(folder), "First launch uses the default without writing settings");
            var gradient = new BackgroundOptions { Kind = BackgroundKind.Gradient, FirstColor = Color.Coral.ToArgb(), SecondColor = Color.LightBlue.ToArgb(), Angle = 125, Lightness = 35 };
            store.Save(gradient, null);
            var restored = new AppearanceStore(folder).Load(out loaded);
            Check(restored.Kind == BackgroundKind.Gradient && restored.FirstColor == gradient.FirstColor && restored.SecondColor == gradient.SecondColor
                && restored.Angle == 125 && restored.Lightness == 35, "Colors, angle and lightness survive a new store instance");

            string original = Path.Combine(root, "original.png");
            using (var bitmap = new Bitmap(40, 20))
            {
                using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Lime);
                bitmap.Save(original, ImageFormat.Png);
            }
            var imageOptions = new BackgroundOptions { Kind = BackgroundKind.Image, Placement = BackgroundPlacement.Fit, Lightness = 0, FirstColor = Color.Red.ToArgb() };
            using (var imported = AppearanceStore.LoadImage(original))
            {
                using (new FileStream(original, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                store.Save(imageOptions, imported);
            }
            File.Delete(original); // This test owns the fixture; application code never deletes the user's source.
            restored = store.Load(out loaded);
            using (loaded)
            {
                Check(restored.Kind == BackgroundKind.Image && loaded != null && loaded.GetPixel(20, 10).G == 255,
                    "The saved background keeps its own image copy after the original is removed");
                using (var rendered = new Bitmap(100, 100))
                using (var graphics = Graphics.FromImage(rendered))
                {
                    GlassDrawing.RenderBackdrop(graphics, new Rectangle(0, 0, 100, 100), restored, loaded);
                    Check(rendered.GetPixel(50, 5).R == 255 && rendered.GetPixel(50, 50).G == 255, "Fit preserves the whole image and fills unused space with the chosen color");
                    restored.Placement = BackgroundPlacement.Fill;
                    GlassDrawing.RenderBackdrop(graphics, new Rectangle(0, 0, 100, 100), restored, loaded);
                    Check(rendered.GetPixel(50, 5).G == 255, "Fill covers the window without empty borders");
                }
                string settings = Path.Combine(folder, "appearance.json");
                string before = File.ReadAllText(settings);
                using (new FileStream(settings, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    bool failed = false;
                    try { store.Save(imageOptions, loaded); }
                    catch (IOException) { failed = true; }
                    Check(failed, "An unwritable settings file reports failure");
                }
                Check(File.ReadAllText(settings) == before && Directory.GetFiles(folder, "background-*.png").Length == 1
                    && Directory.GetFiles(folder, ".appearance-*.tmp").Length == 0, "A failed save preserves the previous background and cleans up its new files");
            }
            store.Save(new BackgroundOptions(), null);
            Check(Directory.GetFiles(folder, "background-*.png").Length == 0 && store.Load(out loaded).Kind == BackgroundKind.Default,
                "Restoring default removes only the previous managed image");
            File.WriteAllText(Path.Combine(folder, "appearance.json"), "{ broken");
            Check(store.Load(out loaded).Kind == BackgroundKind.Default, "Corrupt settings fall back to the default");
            File.WriteAllText(Path.Combine(folder, "appearance.json"), "{\"Kind\":999,\"ImageFile\":\"../original.png\"}");
            Check(store.Load(out loaded).Kind == BackgroundKind.Default && loaded == null, "Invalid modes and image paths are rejected");
            File.WriteAllText(Path.Combine(folder, "appearance.json"), "{\"Kind\":3,\"ImageFile\":\"background-00000000000000000000000000000000.png\"}");
            Check(store.Load(out loaded).Kind == BackgroundKind.Default, "A missing saved image does not prevent startup");

            using (var window = new Form { ClientSize = new Size(100, 100) })
            using (var bitmap = new Bitmap(100, 100))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                BackgroundAppearance.Save(new BackgroundOptions { Kind = BackgroundKind.Solid, FirstColor = Color.Red.ToArgb(), Lightness = 0 }, null, store);
                GlassDrawing.Backdrop(graphics, window);
                Check(bitmap.GetPixel(50, 50).R == 255, "Saved solid color is drawn in a window");
                BackgroundAppearance.Save(new BackgroundOptions { Kind = BackgroundKind.Solid, FirstColor = Color.Blue.ToArgb(), Lightness = 0 }, null, store);
                GlassDrawing.Backdrop(graphics, window);
                Check(bitmap.GetPixel(50, 50).B == 255 && bitmap.GetPixel(50, 50).R == 0, "Saving redraws the existing backdrop cache without resizing or recreating the window");
                int revision = BackgroundAppearance.Revision;
                using (var editor = new PersonalizationForm())
                {
                    typeof(PersonalizationForm).GetMethod("SetPreset", Hidden).Invoke(editor, new object[] { Color.Pink, Color.Yellow });
                    Check(BackgroundAppearance.Current.FirstColor == Color.Blue.ToArgb() && BackgroundAppearance.Revision == revision,
                        "Editing a preset affects only the preview until Save");
                    typeof(PersonalizationForm).GetMethod("ResetDraft", Hidden).Invoke(editor, null);
                }
                Check(BackgroundAppearance.Current.FirstColor == Color.Blue.ToArgb() && BackgroundAppearance.Revision == revision,
                    "Cancel after restoring default leaves the saved appearance unchanged");
            }
            var classic = new BackgroundOptions { Theme = AppearanceTheme.ClassicSilver, Kind = BackgroundKind.Gradient,
                FirstColor = Color.Coral.ToArgb(), SecondColor = Color.LightBlue.ToArgb(), PanelTransparency = 80 };
            store.Save(classic, null);
            restored = store.Load(out loaded);
            Check(restored.Theme == AppearanceTheme.ClassicSilver && restored.Kind == BackgroundKind.Gradient
                && restored.FirstColor == classic.FirstColor && restored.PanelTransparency == 80, "The theme persists without erasing Glass background or panel choices");
            var glass = classic.Copy(); glass.Theme = AppearanceTheme.Glass;
            Check(!glass.SameAppearance(classic), "Changing only the appearance pack is a saveable edit");
            File.WriteAllText(Path.Combine(folder, "appearance.json"), "{\"Kind\":1,\"PanelTransparency\":60}");
            Check(store.Load(out loaded).Theme == AppearanceTheme.Glass && store.Load(out loaded).PanelTransparency == 60,
                "Settings from earlier versions keep Glass and existing preferences");
            File.WriteAllText(Path.Combine(folder, "appearance.json"), "{\"Theme\":999}");
            Check(store.Load(out loaded).Theme == AppearanceTheme.Glass, "Invalid appearance packs fall back safely");
            BackgroundAppearance.Save(glass, null, store);
            using (var editor = new PersonalizationForm { Opacity = 0, ShowInTaskbar = false })
            {
                editor.Show();
                var theme = (ComboBox)typeof(PersonalizationForm).GetField("theme", Hidden).GetValue(editor);
                var save = (Button)typeof(PersonalizationForm).GetField("save", Hidden).GetValue(editor);
                var slider = (TrackBar)typeof(PersonalizationForm).GetField("panelTransparency", Hidden).GetValue(editor);
                Check(theme.Items.Count == 2 && theme.SelectedIndex == 0 && !save.Visible, "Exactly two packs are offered and an unchanged editor hides Save");
                theme.SelectedIndex = 1;
                Check(save.Visible && !slider.Enabled && BackgroundAppearance.Current.Theme == AppearanceTheme.Glass,
                    "Classic is preview-only until Save and retains the Glass slider value");
                theme.SelectedIndex = 0;
                Check(!save.Visible && slider.Enabled && slider.Value == 80, "Returning to the original pack restores its controls and hides Save");
                theme.SelectedIndex = 1;
                UiText.Language = UiLanguage.English;
                editor.RefreshLanguage();
                Check(theme.SelectedIndex == 1 && theme.Text == "Classic · iTunes / iPod", "Language changes retain the selected theme draft");
                editor.Close();
                UiText.Language = UiLanguage.Turkish;
                Check(BackgroundAppearance.Current.Theme == AppearanceTheme.Glass, "Cancelling a pack selection does not change the application");
            }
            Console.WriteLine("PASS: " + checks + " appearance checks; all settings writes used an isolated test directory.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        checks++;
        Console.WriteLine("PASS: " + description);
    }
}
