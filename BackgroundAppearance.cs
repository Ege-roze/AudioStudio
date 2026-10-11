using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ses_uyg
{
    internal enum BackgroundKind { Default, Solid, Gradient, Image }
    internal enum BackgroundPlacement { Fill, Fit, Stretch }
    internal enum AppearanceTheme { Glass, ClassicSilver }

    internal sealed class BackgroundOptions
    {
        public AppearanceTheme Theme { get; set; } = AppearanceTheme.Glass;
        public BackgroundKind Kind { get; set; } = BackgroundKind.Default;
        public int FirstColor { get; set; } = Color.FromArgb(229, 234, 249).ToArgb();
        public int SecondColor { get; set; } = Color.FromArgb(233, 246, 249).ToArgb();
        public int Angle { get; set; } = 40;
        public int Lightness { get; set; } = 20;
        public int PanelTransparency { get; set; } = 25;
        public BackgroundPlacement Placement { get; set; } = BackgroundPlacement.Fill;
        public string ImageFile { get; set; }

        internal BackgroundOptions Copy() { return (BackgroundOptions)MemberwiseClone(); }

        internal bool SameAppearance(BackgroundOptions other)
        {
            // ImageFile is the storage name, not part of the user's visual choices.
            return other != null && Theme == other.Theme && Kind == other.Kind && FirstColor == other.FirstColor && SecondColor == other.SecondColor
                && Angle == other.Angle && Lightness == other.Lightness && Placement == other.Placement
                && PanelTransparency == other.PanelTransparency;
        }

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(AppearanceTheme), Theme) || !Enum.IsDefined(typeof(BackgroundKind), Kind) || !Enum.IsDefined(typeof(BackgroundPlacement), Placement)
                || Angle < 0 || Angle > 360 || Lightness < 0 || Lightness > 80 || PanelTransparency < 0 || PanelTransparency > 100)
                throw new InvalidDataException("Invalid background settings.");
            FirstColor = Color.FromArgb(255, Color.FromArgb(FirstColor)).ToArgb();
            SecondColor = Color.FromArgb(255, Color.FromArgb(SecondColor)).ToArgb();
        }
    }

    internal sealed class AppearanceStore
    {
        private readonly string directory;
        private string SettingsPath { get { return Path.Combine(directory, "appearance.json"); } }
        private static readonly Regex ManagedImage = new Regex(@"\Abackground-[a-f0-9]{32}\.png\z", RegexOptions.CultureInvariant);

        internal AppearanceStore(string directory) { this.directory = Path.GetFullPath(directory); }

        private BackgroundOptions ReadOptions()
        {
            if (!File.Exists(SettingsPath)) return new BackgroundOptions();
            if (new FileInfo(SettingsPath).Length > 32768) throw new InvalidDataException();
            var options = new JavaScriptSerializer().Deserialize<BackgroundOptions>(File.ReadAllText(SettingsPath));
            if (options == null) throw new InvalidDataException();
            options.Validate();
            if (options.ImageFile != null && !ManagedImage.IsMatch(options.ImageFile)) throw new InvalidDataException();
            return options;
        }

        internal BackgroundOptions Load(out Bitmap image)
        {
            image = null;
            try
            {
                var options = ReadOptions();
                if (options.ImageFile != null) image = LoadImage(Path.Combine(directory, options.ImageFile));
                if (options.Kind == BackgroundKind.Image && image == null) return new BackgroundOptions();
                return options;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException
                || ex is InvalidOperationException || ex is FormatException || ex is OverflowException
                || ex is OutOfMemoryException || ex is System.Runtime.InteropServices.ExternalException)
            {
                if (image != null) image.Dispose();
                image = null;
                return new BackgroundOptions();
            }
        }

        internal BackgroundOptions Save(BackgroundOptions value, Image image)
        {
            var options = value.Copy();
            options.Validate();
            if (options.Kind == BackgroundKind.Image && image == null) throw UiText.Error("AppearanceImageRequired");
            Directory.CreateDirectory(directory);
            string oldImage = null;
            try { oldImage = ReadOptions().ImageFile; }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException
                || ex is InvalidOperationException || ex is FormatException || ex is OverflowException) { }
            string temporary = Path.Combine(directory, ".appearance-" + Guid.NewGuid().ToString("N") + ".tmp");
            string newImage = null;
            bool committed = false;
            try
            {
                options.ImageFile = image == null ? null : "background-" + Guid.NewGuid().ToString("N") + ".png";
                if (image != null)
                {
                    newImage = Path.Combine(directory, options.ImageFile);
                    image.Save(newImage, ImageFormat.Png);
                }
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] json = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(options));
                    stream.Write(json, 0, json.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
                else File.Move(temporary, SettingsPath);
                committed = true;
                // Only remove the previous image owned by this settings file.
                if (oldImage != null && ManagedImage.IsMatch(oldImage)) TryDelete(Path.Combine(directory, oldImage));
                return options;
            }
            finally
            {
                TryDelete(temporary);
                if (!committed && newImage != null) TryDelete(newImage);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static Bitmap LoadImage(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var source = Image.FromStream(stream, true, true))
            {
                if ((long)source.Width * source.Height > 40000000) throw UiText.Error("AppearanceImageLarge");
                double scale = Math.Min(1d, 3840d / Math.Max(source.Width, source.Height));
                var bitmap = new Bitmap(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), PixelFormat.Format32bppPArgb);
                try
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, new Rectangle(Point.Empty, bitmap.Size));
                    }
                    return bitmap;
                }
                catch { bitmap.Dispose(); throw; }
            }
        }
    }

    internal static class BackgroundAppearance
    {
        private static readonly AppearanceStore store = new AppearanceStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SesStudyosu"));
        private static BackgroundOptions current;
        private static Bitmap image;
        internal static int Revision { get; private set; }
        internal static BackgroundOptions Current { get { EnsureLoaded(); return current; } }
        internal static Image Image { get { EnsureLoaded(); return image; } }

        private static void EnsureLoaded()
        {
            if (current == null) current = store.Load(out image);
        }

        internal static void Save(BackgroundOptions options, Image selectedImage, AppearanceStore destination = null)
        {
            Bitmap next = selectedImage == null ? null : new Bitmap(selectedImage);
            BackgroundOptions saved;
            try { saved = (destination ?? store).Save(options, next); }
            catch { if (next != null) next.Dispose(); throw; }
            var previous = image;
            image = next;
            current = saved;
            Revision++;
            if (previous != null) previous.Dispose();
            // Changing appearance must not recreate windows or interrupt audio.
            foreach (Form window in Application.OpenForms) { ApplicationTheme.Apply(window); window.Invalidate(true); }
        }
    }
}
