using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace ses_uyg
{
    internal sealed class PersonalizationForm : Form
    {
        private readonly BackgroundOptions initial = BackgroundAppearance.Current.Copy();
        private BackgroundOptions draft;
        private Bitmap selectedImage;
        private readonly string initialImageFingerprint;
        private string selectedImageFingerprint;
        private readonly ComboBox theme = Selector();
        private readonly Label themeHint = TextBlock("AppearanceThemeGlassHint");
        private readonly ComboBox mode = Selector();
        private readonly ComboBox placement = Selector();
        private readonly Button firstColor = Button("AppearanceFirstColor");
        private readonly Button secondColor = Button("AppearanceSecondColor");
        private readonly NumericUpDown angle = new NumericUpDown { Minimum = 0, Maximum = 360, Width = 72 };
        private readonly TrackBar lightness = new TrackBar { Minimum = 0, Maximum = 80, TickStyle = TickStyle.None, AutoSize = false, Height = 32, Dock = DockStyle.Top };
        private readonly Label lightnessLabel = TextBlock("AppearanceLightness");
        private readonly TrackBar panelTransparency = new TrackBar { Name = "panelTransparency", Minimum = 0, Maximum = 100,
            TickStyle = TickStyle.None, AutoSize = false, Height = 32, Dock = DockStyle.Top };
        private readonly Label panelTransparencyLabel = TextBlock("AppearancePanelTransparency");
        private readonly Label panelTransparencyHint = TextBlock("AppearancePanelTransparencyHint");
        private readonly Label imageName = TextBlock("AppearanceNoImage");
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent, ForeColor = Color.Firebrick };
        private readonly PreviewCanvas preview = new PreviewCanvas();
        private readonly Button save = Button("AppearanceSave");
        private Button chooseImage;
        private Control colorPresets;
        private bool updating;

        internal PersonalizationForm()
        {
            draft = initial.Copy();
            if (BackgroundAppearance.Image != null) selectedImage = new Bitmap(BackgroundAppearance.Image);
            initialImageFingerprint = selectedImageFingerprint = ImageFingerprint(selectedImage);
            if (selectedImage != null) UiText.Set(imageName, "AppearanceSavedImage");
            UiText.Set(this, "AppearanceTitle");
            Name = "personalizationForm";
            DoubleBuffered = true;
            ResizeRedraw = true;
            Font = new Font("Segoe UI", 10f);
            ForeColor = Color.FromArgb(38, 48, 77);
            BackColor = Color.FromArgb(230, 237, 250);
            ClientSize = new Size(880, 730);
            MinimumSize = new Size(760, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            SizeGripStyle = SizeGripStyle.Show;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
            Controls.Add(scrollHost);
            scrollHost.Scroll += (s, e) => Invalidate(true);

            var page = Stack();
            page.Padding = new Padding(24);
            scrollHost.Controls.Add(page);
            page.LocationChanged += (s, e) => Invalidate(true);
            var header = GlassPanel.CreateHeader();
            page.Controls.Add(header);
            var title = TextBlock("AppearanceTitle");
            title.Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold);
            header.Controls.Add(title);
            header.Controls.Add(TextBlock("AppearanceDescription"));
            var columns = new GlassLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Margin = new Padding(0, 12, 0, 12) };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            page.Controls.Add(columns);
            var choices = Card();
            choices.Margin = new Padding(0, 0, 8, 0);
            columns.Controls.Add(choices, 0, 0);
            choices.Controls.Add(TextBlock("AppearanceTheme"));
            theme.Name = "appearanceTheme";
            choices.Controls.Add(theme);
            choices.Controls.Add(themeHint);
            choices.Controls.Add(TextBlock("AppearanceMode"));
            mode.Name = "backgroundMode";
            choices.Controls.Add(mode);
            choices.Controls.Add(TextBlock("AppearancePresets"));
            var lavender = Button("AppearanceLavender");
            var ocean = Button("AppearanceOcean");
            var sunset = Button("AppearanceSunset");
            colorPresets = Row(lavender, ocean, sunset);
            choices.Controls.Add(colorPresets);
            lavender.Click += (s, e) => SetPreset(Color.FromArgb(207, 194, 242), Color.FromArgb(239, 220, 243));
            ocean.Click += (s, e) => SetPreset(Color.FromArgb(161, 213, 240), Color.FromArgb(205, 242, 225));
            sunset.Click += (s, e) => SetPreset(Color.FromArgb(255, 205, 176), Color.FromArgb(236, 188, 219));
            choices.Controls.Add(Row(firstColor, secondColor));
            firstColor.Name = "appearanceFirstColor";
            secondColor.Name = "appearanceSecondColor";
            choices.Controls.Add(Row(TextBlock("AppearanceAngle"), angle));
            chooseImage = Button("AppearanceChooseImage");
            chooseImage.Name = "chooseBackgroundImage";
            choices.Controls.Add(chooseImage);
            imageName.AutoSize = false;
            imageName.AutoEllipsis = true;
            imageName.Height = 25;
            choices.Controls.Add(imageName);
            choices.Controls.Add(TextBlock("AppearancePlacement"));
            choices.Controls.Add(placement);
            choices.Controls.Add(lightnessLabel);
            choices.Controls.Add(lightness);
            choices.Controls.Add(TextBlock("AppearanceLightnessHint"));

            var sample = Card();
            sample.Margin = new Padding(8, 0, 0, 0);
            columns.Controls.Add(sample, 1, 0);
            sample.Controls.Add(TextBlock("AppearancePreview"));
            preview.Dock = DockStyle.Top;
            preview.Height = 260;
            preview.Margin = new Padding(0, 10, 0, 12);
            sample.Controls.Add(preview);
            sample.Controls.Add(panelTransparencyLabel);
            sample.Controls.Add(panelTransparency);
            sample.Controls.Add(panelTransparencyHint);
            sample.Controls.Add(TextBlock("AppearancePreviewHint"));
            var footer = Card();
            footer.Dock = DockStyle.Bottom;
            footer.Padding = new Padding(24, 10, 24, 12);
            footer.Controls.Add(status);
            Controls.Add(footer);
            var reset = Button("AppearanceReset");
            var cancel = Button("AppearanceCancel");
            save.Name = "saveAppearance";
            save.Visible = false;
            save.BackColor = Color.FromArgb(102, 86, 213);
            save.ForeColor = Color.White;
            footer.Controls.Add(Row(reset, cancel, save));
            CancelButton = cancel;
            cancel.Click += (s, e) => Close();
            reset.Click += (s, e) => ResetDraft();
            save.Click += (s, e) => SaveAppearance();
            chooseImage.Click += (s, e) => ChooseImage();
            firstColor.Click += (s, e) => ChooseColor(false);
            secondColor.Click += (s, e) => ChooseColor(true);
            theme.SelectedIndexChanged += (s, e) =>
            {
                if (updating || theme.SelectedIndex < 0) return;
                draft.Theme = (AppearanceTheme)theme.SelectedIndex;
                RefreshPreview();
            };
            mode.SelectedIndexChanged += (s, e) =>
            {
                if (updating || mode.SelectedIndex < 0) return;
                draft.Kind = (BackgroundKind)mode.SelectedIndex;
                RefreshPreview();
            };
            placement.SelectedIndexChanged += (s, e) =>
            {
                if (updating || placement.SelectedIndex < 0) return;
                draft.Placement = (BackgroundPlacement)placement.SelectedIndex;
                RefreshPreview();
            };
            angle.ValueChanged += (s, e) => { if (!updating) { draft.Angle = (int)angle.Value; RefreshPreview(); } };
            lightness.ValueChanged += (s, e) => { if (!updating) { draft.Lightness = lightness.Value; RefreshPreview(); } };
            panelTransparency.ValueChanged += (s, e) => { if (!updating) { draft.PanelTransparency = panelTransparency.Value; RefreshPreview(); } };
            RefreshLanguage();
            ApplicationTheme.Apply(this);
        }

        internal void RefreshLanguage()
        {
            updating = true;
            UiText.Apply(this);
            theme.Items.Clear();
            foreach (var key in new[] { "AppearanceThemeGlass", "AppearanceThemeClassic" }) theme.Items.Add(UiText.Get(key));
            theme.SelectedIndex = (int)draft.Theme;
            mode.Items.Clear();
            foreach (var key in new[] { "AppearanceDefault", "AppearanceSolid", "AppearanceGradient", "AppearanceImage" }) mode.Items.Add(UiText.Get(key));
            placement.Items.Clear();
            foreach (var key in new[] { "AppearanceFill", "AppearanceFit", "AppearanceStretch" }) placement.Items.Add(UiText.Get(key));
            mode.SelectedIndex = (int)draft.Kind;
            placement.SelectedIndex = (int)draft.Placement;
            angle.Value = draft.Angle;
            lightness.Value = draft.Lightness;
            panelTransparency.Value = draft.PanelTransparency;
            panelTransparency.AccessibleName = UiText.Get("AppearancePanelTransparency");
            updating = false;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            bool glass = draft.Theme == AppearanceTheme.Glass;
            mode.Enabled = chooseImage.Enabled = colorPresets.Enabled = panelTransparency.Enabled = glass;
            UiText.Set(themeHint, glass ? "AppearanceThemeGlassHint" : "AppearanceThemeClassicHint");
            UiText.Set(panelTransparencyHint, glass ? "AppearancePanelTransparencyHint" : "AppearanceClassicControlsHint");
            bool colors = glass && (draft.Kind == BackgroundKind.Solid || draft.Kind == BackgroundKind.Gradient || draft.Kind == BackgroundKind.Image);
            firstColor.Enabled = colors;
            secondColor.Enabled = angle.Enabled = glass && draft.Kind == BackgroundKind.Gradient;
            placement.Enabled = glass && draft.Kind == BackgroundKind.Image;
            lightness.Enabled = glass && draft.Kind != BackgroundKind.Default;
            foreach (var pair in new[] { Tuple.Create(firstColor, draft.FirstColor), Tuple.Create(secondColor, draft.SecondColor) })
            {
                pair.Item1.BackColor = Color.FromArgb(pair.Item2);
                pair.Item1.ForeColor = pair.Item1.BackColor.GetBrightness() < .5f ? Color.White : Color.FromArgb(38, 48, 77);
            }
            UiText.Set(lightnessLabel, "AppearanceLightnessValue", draft.Lightness);
            UiText.Set(panelTransparencyLabel, "AppearancePanelTransparencyValue", draft.PanelTransparency);
            preview.UpdatePreview(draft, selectedImage);
            bool changed = HasChanges;
            save.Visible = save.Enabled = changed;
            AcceptButton = changed ? save : null;
        }

        private bool HasChanges { get { return !draft.SameAppearance(initial) || selectedImageFingerprint != initialImageFingerprint; } }

        private static string ImageFingerprint(Bitmap value)
        {
            if (value == null) return null;
            // Compare the selected image's contents, including choosing the original image again.
            using (var normalized = new Bitmap(value))
            using (var bytes = new MemoryStream())
            using (var hash = SHA256.Create())
            {
                normalized.Save(bytes, ImageFormat.Png);
                bytes.Position = 0;
                return Convert.ToBase64String(hash.ComputeHash(bytes));
            }
        }

        private void SetPreset(Color first, Color second)
        {
            draft.Kind = BackgroundKind.Gradient;
            draft.FirstColor = first.ToArgb();
            draft.SecondColor = second.ToArgb();
            draft.Angle = 40;
            draft.Lightness = 10;
            RefreshLanguage();
        }

        private void ChooseColor(bool second)
        {
            using (var dialog = new ColorDialog { FullOpen = true, Color = Color.FromArgb(second ? draft.SecondColor : draft.FirstColor) })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (second) draft.SecondColor = dialog.Color.ToArgb();
                else draft.FirstColor = dialog.Color.ToArgb();
                RefreshPreview();
            }
        }

        private void ChooseImage()
        {
            using (var dialog = new OpenFileDialog { Title = UiText.Get("AppearanceChooseImage"), Filter = UiText.Get("AppearanceImageFilter"), CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var next = AppearanceStore.LoadImage(dialog.FileName);
                    string fingerprint;
                    try { fingerprint = ImageFingerprint(next); }
                    catch { next.Dispose(); throw; }
                    var previous = selectedImage;
                    selectedImage = next;
                    selectedImageFingerprint = fingerprint;
                    if (previous != null) previous.Dispose();
                    draft.Kind = BackgroundKind.Image;
                    UiText.SetLiteral(imageName, Path.GetFileName(dialog.FileName));
                    UiText.SetLiteral(status, "");
                    RefreshLanguage();
                }
                catch (Exception ex) { UiText.Set(status, "AppearanceImageError", ex); ScrollControlIntoView(status); }
            }
        }

        private void ResetDraft()
        {
            draft = new BackgroundOptions();
            if (selectedImage != null) { selectedImage.Dispose(); selectedImage = null; }
            selectedImageFingerprint = null;
            UiText.Set(imageName, "AppearanceNoImage");
            UiText.SetLiteral(status, "");
            RefreshLanguage();
        }

        private void SaveAppearance()
        {
            if (!HasChanges) return;
            try { BackgroundAppearance.Save(draft, selectedImage); Close(); }
            catch (Exception ex) { UiText.Set(status, "AppearanceSaveError", ex); ScrollControlIntoView(status); }
        }

        private static ComboBox Selector()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(242, 245, 253), Margin = new Padding(0, 3, 0, 12) };
        }

        private static Label TextBlock(string key)
        {
            return UiText.Bind(new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent, Margin = new Padding(0, 3, 0, 8) }, key);
        }

        private static Button Button(string key)
        {
            return UiText.Bind(new GlassButton { AutoSize = true, MinimumSize = new Size(80, 36), Padding = new Padding(9, 3, 9, 3),
                Margin = new Padding(0, 0, 8, 8), BackColor = Color.FromArgb(226, 230, 249),
                ForeColor = Color.FromArgb(102, 86, 213), Cursor = Cursors.Hand, UseVisualStyleBackColor = false }, key);
        }

        private static GlassLayoutPanel Stack()
        {
            var panel = new GlassLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1, Margin = Padding.Empty };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return panel;
        }

        private static GlassPanel Card()
        {
            var panel = new GlassPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1, Padding = new Padding(20) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return panel;
        }

        private static Control Row(params Control[] controls)
        {
            var row = new GlassFlowPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = Padding.Empty };
            row.Controls.AddRange(controls);
            return row;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { GlassDrawing.Backdrop(e.Graphics, this); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(true); }
        protected override void OnScroll(ScrollEventArgs e) { base.OnScroll(e); Invalidate(true); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(true); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && selectedImage != null) { selectedImage.Dispose(); selectedImage = null; }
            base.Dispose(disposing);
        }

        private sealed class PreviewCanvas : Control
        {
            private BackgroundOptions options;
            private Image image;
            private Bitmap backdrop;
            private Bitmap frosted;
            internal PreviewCanvas() { DoubleBuffered = true; ResizeRedraw = true; }

            internal void UpdatePreview(BackgroundOptions value, Image selected)
            {
                // A transparency-only edit can reuse the existing background and blur.
                var previous = options == null ? null : options.Copy();
                if (previous != null) previous.PanelTransparency = value.PanelTransparency;
                if (previous == null || !previous.SameAppearance(value) || image != selected) ClearBackdrop();
                options = value.Copy();
                image = selected;
                Invalidate();
            }

            private void ClearBackdrop()
            {
                if (backdrop != null) { backdrop.Dispose(); backdrop = null; }
                if (frosted != null) { frosted.Dispose(); frosted = null; }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0) return;
                if (backdrop == null || backdrop.Size != ClientSize)
                {
                    ClearBackdrop();
                    backdrop = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                    using (var canvas = Graphics.FromImage(backdrop))
                        GlassDrawing.RenderBackdrop(canvas, ClientRectangle, options ?? new BackgroundOptions(), image);
                    frosted = GlassDrawing.Frost(backdrop);
                }
                e.Graphics.DrawImageUnscaled(backdrop, Point.Empty);
                if (Width < 100 || Height < 100) return;
                if (options != null && options.Theme == AppearanceTheme.ClassicSilver) { DrawClassicPreview(e.Graphics); return; }
                var state = e.Graphics.Save();
                e.Graphics.TranslateTransform(16, 24);
                GlassDrawing.PanelPreview(e.Graphics, new Size(Width - 32, Height - 48), frosted, new Point(16, 24),
                    options == null ? 25 : options.PanelTransparency);
                e.Graphics.Restore(state);
                using (var font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold))
                    TextRenderer.DrawText(e.Graphics, UiText.Get("AppTitle"), font,
                        new Rectangle(32, 48, Width - 64, 70), Color.FromArgb(38, 48, 77), TextFormatFlags.WordBreak);
                TextRenderer.DrawText(e.Graphics, UiText.Get("AppearanceSample"), Font,
                    new Rectangle(32, 125, Width - 64, Height - 145), Color.FromArgb(70, 83, 110), TextFormatFlags.WordBreak);
            }

            private void DrawClassicPreview(Graphics g)
            {
                var state = g.Save();
                g.TranslateTransform(12, 16);
                GlassDrawing.ClassicPanel(g, new Size(Width - 24, 86), true);
                g.Restore(state);
                using (var title = new Font("Segoe UI Semibold", 15, FontStyle.Bold))
                    TextRenderer.DrawText(g, UiText.Get("AppTitle"), title, new Rectangle(30, 32, Width - 60, 36),
                        Color.FromArgb(62, 73, 50), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, "◀   ▶   ▶▶", Font, new Rectangle(30, 65, Width - 60, 22),
                    Color.FromArgb(62, 73, 50), TextFormatFlags.HorizontalCenter);
                state = g.Save();
                g.TranslateTransform(12, 106);
                GlassDrawing.ClassicPanel(g, new Size(Width - 24, Height - 118), false);
                g.Restore(state);
                TextRenderer.DrawText(g, UiText.Get("AiTitle"), Font, new Rectangle(28, 119, Width - 56, 28), ApplicationTheme.Ink);
                var selected = new Rectangle(25, 153, Width - 55, 30);
                using (var blue = new System.Drawing.Drawing2D.LinearGradientBrush(selected, Color.FromArgb(115, 164, 216), ApplicationTheme.Blue, 90f))
                    g.FillRectangle(blue, selected);
                TextRenderer.DrawText(g, UiText.Get("AiEnable"), Font, selected, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, UiText.Get("Effects") + "  /  " + UiText.Get("Library"), Font,
                    new Rectangle(28, 195, Width - 56, 38), ApplicationTheme.Ink, TextFormatFlags.WordBreak);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) ClearBackdrop();
                base.Dispose(disposing);
            }
        }
    }
}
