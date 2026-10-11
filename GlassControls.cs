using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ses_uyg
{
    // Painted glass surfaces keep this theme independent of OS backdrop support.
    internal static class GlassDrawing
    {
        private static readonly ConditionalWeakTable<Control, BackdropCache> backdrops =
            new ConditionalWeakTable<Control, BackdropCache>();

        private sealed class BackdropCache : IDisposable
        {
            private Bitmap bitmap;
            private Bitmap frosted;
            private int revision = -1;

            private void Ensure(Size size)
            {
                if (size.Width <= 0 || size.Height <= 0) return;
                if (bitmap == null || bitmap.Size != size || revision != BackgroundAppearance.Revision)
                {
                    // One rasterization per window size, shared by all children.
                    var next = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                    try
                    {
                        using (var canvas = Graphics.FromImage(next))
                            RenderBackdrop(canvas, new Rectangle(Point.Empty, size), BackgroundAppearance.Current, BackgroundAppearance.Image);
                    }
                    catch { next.Dispose(); throw; }
                    if (bitmap != null) bitmap.Dispose();
                    if (frosted != null) { frosted.Dispose(); frosted = null; }
                    bitmap = next;
                    revision = BackgroundAppearance.Revision;
                }
            }

            internal void Draw(Graphics g, Size size)
            {
                Ensure(size);
                if (bitmap != null) g.DrawImageUnscaled(bitmap, Point.Empty);
            }

            internal void DrawFrosted(Graphics g, Size size, Point offset, float opacity)
            {
                Ensure(size);
                if (bitmap == null) return;
                if (frosted == null) frosted = Frost(bitmap);
                DrawFrostedImage(g, frosted, offset, opacity);
            }

            public void Dispose()
            {
                if (bitmap != null) { bitmap.Dispose(); bitmap = null; }
                if (frosted != null) { frosted.Dispose(); frosted = null; }
            }
        }

        internal static void Backdrop(Graphics g, Control window)
        {
            GetBackdrop(window).Draw(g, window.ClientSize);
        }

        private static BackdropCache GetBackdrop(Control window)
        {
            return backdrops.GetValue(window, owner =>
            {
                var entry = new BackdropCache();
                owner.Disposed += (s, e) => entry.Dispose();
                return entry;
            });
        }

        internal static Bitmap Frost(Bitmap source)
        {
            // Downsample once per window size/background revision, never per audio frame.
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
            try
            {
                using (var small = new Bitmap(Math.Max(2, source.Width / 14), Math.Max(2, source.Height / 14), PixelFormat.Format32bppPArgb))
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    using (var g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(source, new Rectangle(Point.Empty, small.Size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
                    }
                    using (var g = Graphics.FromImage(result))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(small, new Rectangle(Point.Empty, result.Size), 0, 0, small.Width, small.Height, GraphicsUnit.Pixel, attributes);
                    }
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        // WinForms transparency copies a parent's visible pixels. That copy is
        // incomplete when a tall parent is clipped by a scrolling viewport.
        // Rebuild the background in ancestor coordinates instead of copying it.
        internal static void ParentSurface(Control control, Graphics g)
        {
            var parent = control.Parent;
            if (parent == null) return;
            var state = g.Save();
            try
            {
                g.TranslateTransform(-control.Left, -control.Top);
                if (parent is Form)
                    Backdrop(g, parent);
                else if (parent is GlassPanel)
                    ((GlassPanel)parent).DrawSurface(g);
                else
                    ParentSurface(parent, g);
            }
            finally { g.Restore(state); }
        }

        internal static void PanelSurface(Graphics g, Size size)
        {
            if (ApplicationTheme.Classic) { ClassicPanel(g, size, false); return; }
            PanelSurface(g, size, BackgroundAppearance.Current.PanelTransparency, null);
        }

        internal static void PanelSurface(Graphics g, Control panel)
        {
            if (ApplicationTheme.Classic) { ClassicPanel(g, panel.ClientSize, panel is GlassPanel && ((GlassPanel)panel).IsHeader); return; }
            var window = panel.FindForm();
            PanelSurface(g, panel.ClientSize, BackgroundAppearance.Current.PanelTransparency,
                window == null ? (Action<Graphics, float>)null : (canvas, opacity) =>
                    GetBackdrop(window).DrawFrosted(canvas, window.ClientSize, WindowOffset(panel), opacity));
        }

        internal static void PanelPreview(Graphics g, Size size, Bitmap frosted, Point offset, int transparency, AppearanceTheme theme = AppearanceTheme.Glass)
        {
            if (theme == AppearanceTheme.ClassicSilver) { ClassicPanel(g, size, false); return; }
            PanelSurface(g, size, transparency, (canvas, opacity) => DrawFrostedImage(canvas, frosted, offset, opacity));
        }

        internal static void ClassicPanel(Graphics g, Size size, bool header)
        {
            if (size.Width < 12 || size.Height < 12) return;
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new Rectangle(2, 2, size.Width - 7, size.Height - 9);
                using (var shadow = Round(new Rectangle(3, 5, bounds.Width, bounds.Height), 6))
                using (var brush = new SolidBrush(Color.FromArgb(45, 35, 39, 43))) g.FillPath(brush, shadow);
                using (var path = Round(bounds, 6))
                using (var fill = new LinearGradientBrush(bounds, Color.FromArgb(251, 252, 253), Color.FromArgb(229, 232, 235), 90f))
                using (var rim = new Pen(Color.FromArgb(130, 137, 144)))
                {
                    g.FillPath(fill, path);
                    var clipped = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    if (header)
                    {
                        var lcd = Rectangle.Inflate(bounds, -8, -8);
                        using (var screen = new LinearGradientBrush(lcd, Color.FromArgb(226, 232, 207), Color.FromArgb(196, 207, 173), 90f))
                            g.FillRectangle(screen, lcd);
                        using (var scan = new Pen(Color.FromArgb(14, 67, 78, 52)))
                            for (int y = lcd.Top + 2; y < lcd.Bottom; y += 3) g.DrawLine(scan, lcd.Left, y, lcd.Right, y);
                        using (var edge = new Pen(Color.FromArgb(132, 144, 115))) g.DrawRectangle(edge, lcd);
                        using (var light = new Pen(Color.FromArgb(246, 249, 229)))
                            g.DrawLine(light, lcd.Left + 1, lcd.Top + 1, lcd.Right - 1, lcd.Top + 1);
                    }
                    else if (size.Height > 130)
                    {
                        var strip = new Rectangle(bounds.X, bounds.Y, bounds.Width, 54);
                        using (var chrome = new LinearGradientBrush(strip, Color.FromArgb(251, 251, 252), Color.FromArgb(211, 216, 221), 90f))
                            g.FillRectangle(chrome, strip);
                        using (var line = new Pen(Color.FromArgb(184, 190, 196))) g.DrawLine(line, bounds.Left, strip.Bottom, bounds.Right, strip.Bottom);
                        using (var light = new Pen(Color.White)) g.DrawLine(light, bounds.Left, strip.Bottom + 1, bounds.Right, strip.Bottom + 1);
                    }
                    g.Restore(clipped);
                    g.DrawPath(rim, path);
                    using (var inner = Round(RectangleF.Inflate(bounds, -1, -1), 5))
                    using (var light = new Pen(Color.FromArgb(210, Color.White))) g.DrawPath(light, inner);
                }
            }
            finally { g.Restore(state); }
        }

        private static void ClassicBackdrop(Graphics g, Rectangle bounds)
        {
            using (var fill = new LinearGradientBrush(bounds, Color.FromArgb(232, 234, 236), Color.FromArgb(173, 179, 186), 90f))
                g.FillRectangle(fill, bounds);
            using (var dark = new Pen(Color.FromArgb(14, 60, 68, 77)))
            using (var light = new Pen(Color.FromArgb(44, Color.White)))
                for (int y = bounds.Top; y < bounds.Bottom; y += 4)
                {
                    g.DrawLine(light, bounds.Left, y, bounds.Right, y);
                    g.DrawLine(dark, bounds.Left, y + 2, bounds.Right, y + 2);
                }
        }

        private static void DrawFrostedImage(Graphics g, Bitmap frosted, Point offset, float opacity)
        {
            // Keep the frosted backdrop underneath the glass, including at maximum effect.
            var target = new Rectangle(-offset.X + 2, -offset.Y + 1, frosted.Width, frosted.Height);
            if (opacity >= 1f) { g.DrawImageUnscaled(frosted, target.Location); return; }
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
                g.DrawImage(frosted, target, 0, 0, frosted.Width, frosted.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private static int TintAlpha(int original, int transparency)
        {
            // The upper end is translucent glass, never an invisible panel.
            // Retain the previous appearance at 25% so existing preferences stay familiar.
            return (int)Math.Round(transparency <= 25 ? 255 - (255 - original) * transparency / 25f
                : original * (1f - .20f * (transparency - 25) / 75f));
        }

        private static void PanelSurface(Graphics g, Size size, int transparency, Action<Graphics, float> drawFrosted)
        {
            if (size.Width < 12 || size.Height < 12) return;
            float glass = Math.Max(0f, (transparency - 25) / 75f);
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var shadow = Round(new RectangleF(3, 6, size.Width - 6, size.Height - 7), 24))
                using (var brush = new SolidBrush(Color.FromArgb((int)(35 + 7 * glass), 24, 55, 92)))
                    g.FillPath(brush, shadow);
                var bounds = new Rectangle(2, 2, size.Width - 7, size.Height - 9);
                using (var path = Round(bounds, 24))
                using (var fill = new LinearGradientBrush(bounds, Color.White, Color.White, 90f))
                using (var rim = new Pen(Color.FromArgb((int)(105 + 35 * glass), 74, 134, 181), 1.1f))
                {
                    var clipped = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    if (drawFrosted != null && transparency > 0) drawFrosted(g, 1f);
                    fill.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.FromArgb(TintAlpha(215, transparency), 255, 255, 255), Color.FromArgb(TintAlpha(180, transparency), 245, 251, 255),
                            Color.FromArgb(TintAlpha(165, transparency), 232, 244, 255), Color.FromArgb(TintAlpha(205, transparency), 251, 255, 255) },
                        Positions = new[] { 0f, .24f, .68f, 1f }
                    };
                    g.FillPath(fill, path);
                    // A broad curved reflection and a faint diagonal light band give the surface depth.
                    using (var sheen = new LinearGradientBrush(bounds, Color.FromArgb((int)(66 + 28 * glass), Color.White), Color.FromArgb(0, Color.White), 90f))
                        g.FillEllipse(sheen, -size.Width * .18f, -size.Height * .43f, size.Width * 1.38f, size.Height * .77f);
                    using (var reflection = new SolidBrush(Color.FromArgb((int)(20 + 14 * glass), Color.White)))
                        g.FillPolygon(reflection, new[] { new PointF(size.Width * .62f, 0), new PointF(size.Width * .88f, 0),
                            new PointF(size.Width * .34f, size.Height), new PointF(size.Width * .20f, size.Height) });
                    g.Restore(clipped);
                    g.DrawPath(rim, path);
                    using (var inner = Round(RectangleF.Inflate(bounds, -1.3f, -1.3f), 22.5f))
                    using (var light = new LinearGradientBrush(bounds, Color.FromArgb(255, Color.White), Color.FromArgb((int)(100 + 75 * glass), 211, 249, 255), 75f))
                    using (var edge = new Pen(light, 1.6f)) g.DrawPath(edge, inner);
                    using (var bottom = new Pen(Color.FromArgb((int)(160 + 45 * glass), 255, 255, 255), 1.2f))
                        g.DrawLine(bottom, bounds.Left + 28, bounds.Bottom - 2, bounds.Right - 28, bounds.Bottom - 2);
                }
            }
            finally { g.Restore(state); }
        }

        internal static GraphicsPath Round(RectangleF bounds, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static Point WindowOffset(Control control)
        {
            var offset = Point.Empty;
            for (var current = control; current != null && !(current is Form); current = current.Parent)
                offset.Offset(current.Location);
            return offset;
        }

        internal static Color Mix(Color first, Color second, float amount)
        {
            return Color.FromArgb((int)(first.R + (second.R - first.R) * amount),
                (int)(first.G + (second.G - first.G) * amount), (int)(first.B + (second.B - first.B) * amount));
        }

        internal static void RenderBackdrop(Graphics g, Rectangle bounds, BackgroundOptions options, Image image)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            if (options.Theme == AppearanceTheme.ClassicSilver) { ClassicBackdrop(g, bounds); return; }
            if (options.Kind == BackgroundKind.Default) { RenderDefaultBackdrop(g, bounds); return; }
            var first = Color.FromArgb(options.FirstColor);
            using (var brush = new SolidBrush(first)) g.FillRectangle(brush, bounds);
            if (options.Kind == BackgroundKind.Gradient)
            {
                using (var brush = new LinearGradientBrush(bounds, first, Color.FromArgb(options.SecondColor), options.Angle))
                    g.FillRectangle(brush, bounds);
            }
            else if (options.Kind == BackgroundKind.Image && image != null)
            {
                var target = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                if (options.Placement != BackgroundPlacement.Stretch)
                {
                    float x = bounds.Width / (float)image.Width, y = bounds.Height / (float)image.Height;
                    float scale = options.Placement == BackgroundPlacement.Fill ? Math.Max(x, y) : Math.Min(x, y);
                    target = new RectangleF(bounds.X + (bounds.Width - image.Width * scale) / 2,
                        bounds.Y + (bounds.Height - image.Height * scale) / 2, image.Width * scale, image.Height * scale);
                }
                var state = g.Save();
                try
                {
                    g.SetClip(bounds);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, target);
                }
                finally { g.Restore(state); }
            }
            if (options.Lightness > 0)
                using (var brush = new SolidBrush(Color.FromArgb(options.Lightness * 255 / 100, Color.White)))
                    g.FillRectangle(brush, bounds);
        }

        private static void RenderDefaultBackdrop(Graphics g, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (var brush = new LinearGradientBrush(bounds, Color.FromArgb(182, 214, 241), Color.FromArgb(227, 247, 252), 55f))
                g.FillRectangle(brush, bounds);
            Glow(g, new Rectangle(-180, -200, bounds.Width, 700), Color.FromArgb(145, 112, 153, 228));
            Glow(g, new Rectangle(bounds.Width / 2, 80, bounds.Width, 780), Color.FromArgb(135, 70, 213, 235));
            Glow(g, new Rectangle(0, bounds.Height / 2, bounds.Width, 600), Color.FromArgb(85, 178, 159, 235));
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ribbon = new GraphicsPath())
            {
                float w = bounds.Width, h = bounds.Height;
                ribbon.AddBezier(-w * .12f, h * .66f, w * .26f, h * .08f, w * .70f, h * .90f, w * 1.12f, h * .19f);
                using (var haze = new Pen(Color.FromArgb(34, Color.White), 65)) g.DrawPath(haze, ribbon);
                using (var edge = new Pen(Color.FromArgb(95, Color.White), 1.3f)) g.DrawPath(edge, ribbon);
            }
            g.Restore(state);
        }

        private static void Glow(Graphics g, Rectangle bounds, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(bounds);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = color;
                    brush.SurroundColors = new[] { Color.FromArgb(0, color) };
                    g.FillPath(brush, path);
                }
            }
        }
    }

    internal class GlassLayoutPanel : TableLayoutPanel
    {
        internal GlassLayoutPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            GlassDrawing.ParentSurface(this, e.Graphics);
        }
    }

    internal sealed class GlassPanel : GlassLayoutPanel
    {
        internal bool IsHeader { get; set; }
        private Bitmap surface;
        private Point surfaceOffset;
        private Size windowSize;
        private int revision = -1;

        internal static GlassPanel CreateHeader()
        {
            var header = new GlassPanel { IsHeader = true, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, Padding = new Padding(20, 14, 20, 16), Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return header;
        }

        internal void DrawSurface(Graphics g)
        {
            if (Width <= 0 || Height <= 0) return;
            var window = FindForm();
            var size = window == null ? Size.Empty : window.ClientSize;
            var offset = GlassDrawing.WindowOffset(this);
            if (surface == null || surface.Size != ClientSize || windowSize != size
                || surfaceOffset != offset || revision != BackgroundAppearance.Revision)
            {
                var next = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var canvas = Graphics.FromImage(next))
                    {
                        GlassDrawing.ParentSurface(this, canvas);
                        GlassDrawing.PanelSurface(canvas, this);
                    }
                }
                catch { next.Dispose(); throw; }
                if (surface != null) surface.Dispose();
                surface = next;
                surfaceOffset = offset;
                windowSize = size;
                revision = BackgroundAppearance.Revision;
            }
            g.DrawImageUnscaled(surface, Point.Empty);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            DrawSurface(e.Graphics);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && surface != null) { surface.Dispose(); surface = null; }
            base.Dispose(disposing);
        }
    }

    internal sealed class GlassFlowPanel : FlowLayoutPanel
    {
        internal GlassFlowPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            ResizeRedraw = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            GlassDrawing.ParentSurface(this, e.Graphics);
        }
    }

    internal sealed class GlassButton : Button
    {
        private bool hovered;
        private bool pressed;

        internal GlassButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 9 || Height < 9) return;
            GlassDrawing.ParentSurface(this, e.Graphics);
            if (ApplicationTheme.Classic) { PaintClassic(e.Graphics); return; }
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(2, 2, Width - 5, Height - 5);
            var tint = Enabled ? BackColor : Color.FromArgb(226, 232, 242);
            bool colored = tint.GetBrightness() < .72f;
            using (var path = GlassDrawing.Round(bounds, 13))
            using (var fill = new LinearGradientBrush(bounds, tint, tint, 90f))
            using (var edge = new Pen(Color.FromArgb(Enabled && hovered ? 190 : 95, 69, 117, 171), 1f))
            {
                fill.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { GlassDrawing.Mix(tint, Color.White, pressed ? .08f : hovered && Enabled ? .42f : colored ? .24f : .60f),
                        GlassDrawing.Mix(tint, Color.White, pressed ? .02f : colored ? .08f : .32f),
                        GlassDrawing.Mix(tint, colored ? Color.Black : Color.FromArgb(115, 178, 216), pressed ? .18f : colored ? .05f : .13f), tint },
                    Positions = new[] { 0f, .38f, .43f, 1f }
                };
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(edge, path);
                using (var inner = GlassDrawing.Round(RectangleF.Inflate(bounds, -1.2f, -1.2f), 11.8f))
                using (var highlight = new LinearGradientBrush(bounds, Color.FromArgb(230, Color.White), Color.FromArgb(50, Color.White), 90f))
                using (var light = new Pen(highlight, 1f)) e.Graphics.DrawPath(light, inner);
                using (var glint = new Pen(Color.FromArgb(pressed ? 40 : 130, Color.White), 1f))
                    e.Graphics.DrawLine(glint, bounds.Left + 13, bounds.Bottom - 2, bounds.Right - 13, bounds.Bottom - 2);
            }
            var textBounds = Rectangle.Inflate(bounds, -6, -3);
            if (pressed) textBounds.Offset(0, 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, Enabled ? ForeColor : Color.FromArgb(133, 145, 167),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
            if (Focused && ShowFocusCues)
                using (var path = GlassDrawing.Round(Rectangle.Inflate(bounds, -3, -3), 10))
                using (var pen = new Pen(ForeColor) { DashStyle = DashStyle.Dot })
                    e.Graphics.DrawPath(pen, path);
        }

        private void PaintClassic(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(2, 2, Width - 5, Height - 5);
            Color tint = Enabled ? BackColor : Color.FromArgb(224, 226, 228);
            bool action = tint.GetBrightness() < .72f;
            using (var path = GlassDrawing.Round(bounds, 6))
            using (var fill = new LinearGradientBrush(bounds, tint, tint, 90f))
            using (var edge = new Pen(Enabled && hovered ? ApplicationTheme.Blue : Color.FromArgb(128, 137, 146)))
            {
                fill.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { GlassDrawing.Mix(tint, Color.White, pressed ? .05f : action ? .5f : .85f),
                        GlassDrawing.Mix(tint, Color.White, pressed ? 0 : .25f),
                        GlassDrawing.Mix(tint, Color.Black, pressed ? .16f : .06f),
                        GlassDrawing.Mix(tint, Color.White, .16f) },
                    Positions = new[] { 0f, .47f, .51f, 1f }
                };
                g.FillPath(fill, path); g.DrawPath(edge, path);
                using (var inset = GlassDrawing.Round(RectangleF.Inflate(bounds, -1, -1), 5))
                using (var shine = new Pen(Color.FromArgb(140, Color.White))) g.DrawPath(shine, inset);
            }
            var text = Rectangle.Inflate(bounds, -6, -3);
            if (pressed) text.Offset(0, 1);
            TextRenderer.DrawText(g, Text, Font, text, Enabled ? ForeColor : Color.FromArgb(143, 148, 153),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(bounds, -4, -4));
        }
    }
}
