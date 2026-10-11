using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ses_uyg
{
    internal sealed class AboutForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(102, 86, 213);

        internal AboutForm()
        {
            SuspendLayout();
            UiText.Set(this, "LegalTitle");
            Name = "sourcesAndRightsForm";
            DoubleBuffered = true;
            ResizeRedraw = true;
            Font = new Font("Segoe UI", 10f);
            ForeColor = Color.FromArgb(38, 48, 77);
            BackColor = Color.FromArgb(230, 237, 250);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(720, 640);
            MinimumSize = new Size(580, 500);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            AutoScroll = true;

            var page = new GlassLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, Padding = new Padding(28), BackColor = Color.Transparent
            };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(page);
            page.LocationChanged += (s, e) => Invalidate(true);
            var header = GlassPanel.CreateHeader();
            header.Margin = new Padding(0, 0, 0, 14);
            page.Controls.Add(header);
            var brand = TextBlock("Brand");
            brand.Font = new Font("Segoe UI Semibold", 8f);
            brand.ForeColor = Accent;
            brand.Margin = new Padding(0, 0, 0, 6);
            header.Controls.Add(brand);
            var heading = TextBlock("LegalTitle");
            heading.Font = new Font("Segoe UI Semibold", 25f, FontStyle.Bold);
            heading.Margin = new Padding(0, 0, 0, 4);
            header.Controls.Add(heading);

            var card = new GlassPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, Padding = new Padding(24), Margin = Padding.Empty
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(LegalDocumentForm.CreateLink("PrivacyPolicy", 0));
            card.Controls.Add(LegalDocumentForm.CreateLink("TermsOfService", 1));
            card.Controls.Add(LegalDocumentForm.CreateLink("ThirdPartyNotices", 2));
            card.Controls.Add(LegalDocumentForm.CreateLink("ModelTerms", 3));
            foreach (string key in new[] { "LegalIntro", "LegalModel", "LegalCable", "LegalRights" })
                card.Controls.Add(TextBlock(key));
            var linksHeading = TextBlock("LegalLinks");
            linksHeading.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            linksHeading.Margin = new Padding(0, 6, 0, 10);
            card.Controls.Add(linksHeading);
            var links = new GlassFlowPanel
            {
                Name = "legalSources", Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent,
                Margin = Padding.Empty, FlowDirection = FlowDirection.TopDown, WrapContents = false
            };
            links.Controls.Add(SourceLink("LegalVcClient", "https://github.com/w-okada/voice-changer/blob/v.1.5.3.18a/LICENSE"));
            links.Controls.Add(SourceLink("LegalNAudio", "https://github.com/naudio/NAudio/blob/release/2.x/license.txt"));
            links.Controls.Add(SourceLink("LegalModelLink", "https://huggingface.co/wok000/vcclient_model/blob/main/rvc_v2_alpha/tsukuyomi-chan/terms_of_use.txt"));
            links.Controls.Add(SourceLink("LegalCableLink", "https://vb-audio.com/Services/licensing.htm"));
            card.Controls.Add(links);
            page.Controls.Add(card);

            var close = UiText.Bind(new GlassButton
            {
                Name = "legalCloseButton", AutoSize = true, MinimumSize = new Size(90, 38),
                Anchor = AnchorStyles.Right, BackColor = Accent, ForeColor = Color.White,
                UseVisualStyleBackColor = false, Cursor = Cursors.Hand,
                Margin = new Padding(0, 18, 0, 0), Padding = new Padding(12, 4, 12, 4)
            }, "LegalClose");
            close.Click += (s, e) => Close();
            CancelButton = close;
            page.Controls.Add(close);
            ResumeLayout(true);
            ApplicationTheme.Apply(this);
        }

        private static Label TextBlock(string key)
        {
            return UiText.Bind(new Label
            {
                Name = key, AutoSize = true, Dock = DockStyle.Top,
                BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 14)
            }, key);
        }

        private LinkLabel SourceLink(string key, string address)
        {
            var link = UiText.Bind(new LinkLabel
            {
                Name = key, AutoSize = true, BackColor = Color.Transparent,
                LinkColor = Accent, ActiveLinkColor = ForeColor, TabStop = true,
                LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 0, 0, 9)
            }, key);
            link.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, UiText.Get("PageError", ex), UiText.Get("LegalTitle")); }
            };
            return link;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { GlassDrawing.Backdrop(e.Graphics, this); }
        protected override void OnScroll(ScrollEventArgs e) { base.OnScroll(e); Invalidate(true); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(true); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(true); }
    }
}
