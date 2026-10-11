using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ses_uyg
{
    // Keep each control's original palette so returning to Glass is lossless.
    internal static class ApplicationTheme
    {
        private sealed class Palette
        {
            internal Color Back, Fore, Link, ActiveLink, VisitedLink;
        }
        private static readonly ConditionalWeakTable<Control, Palette> original = new ConditionalWeakTable<Control, Palette>();
        internal static bool Classic { get { return BackgroundAppearance.Current.Theme == AppearanceTheme.ClassicSilver; } }
        internal static readonly Color Blue = Color.FromArgb(39, 99, 167);
        internal static readonly Color Ink = Color.FromArgb(46, 50, 54);

        internal static void Apply(Control root)
        {
            var controls = new List<Control>();
            Collect(root, controls);
            // Capture the whole tree before changing inherited colors on a parent.
            foreach (var control in controls)
                original.GetValue(control, c =>
                {
                    var link = c as LinkLabel;
                    return new Palette { Back = c.BackColor, Fore = c.ForeColor,
                        Link = link == null ? Color.Empty : link.LinkColor,
                        ActiveLink = link == null ? Color.Empty : link.ActiveLinkColor,
                        VisitedLink = link == null ? Color.Empty : link.VisitedLinkColor };
                });
            bool classic = Classic;
            foreach (var control in controls)
            {
                if (control.Name == "appearanceFirstColor" || control.Name == "appearanceSecondColor") continue;
                var colors = original.GetValue(control, c => new Palette());
                control.ForeColor = classic ? Foreground(colors.Fore) : colors.Fore;
                if (colors.Back.A != 0)
                {
                    bool field = control is TextBoxBase || control is ListBox || control is ComboBox || control is NumericUpDown;
                    control.BackColor = !classic ? colors.Back : field ? Color.FromArgb(251, 252, 253)
                        : control is Button ? ButtonTint(colors.Back) : Color.FromArgb(226, 228, 230);
                }
                var link = control as LinkLabel;
                if (link != null)
                {
                    link.LinkColor = classic ? Blue : colors.Link;
                    link.ActiveLinkColor = classic ? Color.FromArgb(17, 64, 116) : colors.ActiveLink;
                    link.VisitedLinkColor = classic ? Blue : colors.VisitedLink;
                }
            }
        }

        private static Color Foreground(Color color)
        {
            // Preserve white action labels and red error/destructive-action text.
            if (color.GetBrightness() > .9f || color.R > color.G * 1.35 && color.R > color.B * 1.35) return color;
            return color.B > color.R + 35 && color.R > color.G ? Blue : Ink;
        }
        private static Color ButtonTint(Color color)
        {
            if (color.R > color.G + 12 && color.R > color.B + 12) return Color.FromArgb(242, 222, 222);
            return color.GetBrightness() < .72f ? Color.FromArgb(57, 112, 177) : Color.FromArgb(222, 224, 226);
        }
        private static void Collect(Control root, List<Control> controls)
        {
            controls.Add(root);
            foreach (Control child in root.Controls) Collect(child, controls);
        }
    }
}
