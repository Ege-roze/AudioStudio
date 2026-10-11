using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace ses_uyg
{
    // Read the same generated text that is exported for HTTPS hosting. No web view,
    // navigation or microphone access is needed to read the policies.
    internal sealed class LegalDocumentForm : Form
    {
        private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill, Multiline = true };
        private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
        private readonly Button close = new Button { AutoSize = true, MinimumSize = new Size(90, 34) };
        private readonly RichTextBox[] bodies = new RichTextBox[4];
        private static readonly string[] Documents = { "privacy-policy", "terms-of-service", "third-party-notices", "model-terms" };
        private static readonly string[,] Titles = {
            { "Gizlilik Politikası", "Kullanım Koşulları", "Üçüncü Taraf Bildirimleri", "Ses Modeli Koşulları" },
            { "Privacy Policy", "Terms of Service", "Third-Party Notices", "Voice Model Terms" }
        };

        internal LegalDocumentForm(int selectedDocument = 0)
        {
            Name = "legalDocumentsForm";
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(900, 680);
            MinimumSize = new Size(560, 400);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            Padding = new Padding(16);
            AutoScaleMode = AutoScaleMode.Font;
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
            close.Click += (s, e) => Close();
            close.DialogResult = DialogResult.Cancel;
            CancelButton = close;
            language.Name = "legalLanguage";
            language.Items.AddRange(new object[] { "Türkçe", "English" });
            footer.Controls.Add(close);
            footer.Controls.Add(language);
            Controls.Add(tabs);
            Controls.Add(footer);
            for (int i = 0; i < Documents.Length; i++)
            {
                var page = new TabPage { Padding = new Padding(12) };
                var body = new RichTextBox { Name = Documents[i], Dock = DockStyle.Fill,
                    ReadOnly = true, DetectUrls = true, WordWrap = true, BorderStyle = BorderStyle.None,
                    BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText,
                    Font = new Font("Segoe UI", 10.5f), ScrollBars = RichTextBoxScrollBars.Vertical };
                body.LinkClicked += OpenSource;
                bodies[i] = body;
                page.Controls.Add(body);
                tabs.TabPages.Add(page);
            }
            language.SelectedIndexChanged += (s, e) => LoadLanguage();
            language.SelectedIndex = UiText.Language == UiLanguage.English ? 1 : 0;
            tabs.SelectedIndex = Math.Max(0, Math.Min(selectedDocument, Documents.Length - 1));
            ApplicationTheme.Apply(this);
        }

        internal static string ReadDocument(string document, string languageCode)
        {
            if (Array.IndexOf(Documents, document) < 0 || (languageCode != "tr" && languageCode != "en"))
                throw new ArgumentException("Unknown legal document.");
            string name = "ses_uyg.Legal." + document + "." + languageCode + ".txt";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("Missing legal document: " + name);
                using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
            }
        }

        private void LoadLanguage()
        {
            int selected = language.SelectedIndex;
            if (selected < 0) return;
            Text = selected == 0 ? "Gizlilik, Koşullar ve Lisanslar" : "Privacy, Terms and Licenses";
            close.Text = selected == 0 ? "Kapat" : "Close";
            language.AccessibleName = selected == 0 ? "Belge dili" : "Document language";
            for (int i = 0; i < Documents.Length; i++)
            {
                tabs.TabPages[i].Text = Titles[selected, i];
                bodies[i].AccessibleName = Titles[selected, i];
                bodies[i].Text = ReadDocument(Documents[i], selected == 0 ? "tr" : "en");
                bodies[i].Select(0, 0);
            }
        }

        private void OpenSource(object sender, LinkClickedEventArgs e)
        {
            Uri uri;
            // Embedded documents are trusted, but never dispatch arbitrary protocols.
            if (!Uri.TryCreate(e.LinkText, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, UiText.Get("PageError", ex), Text); }
        }

        internal static void ShowDocument(IWin32Window owner, int selectedDocument)
        {
            var parent = owner as Form;
            if (parent != null)
                foreach (var owned in parent.OwnedForms)
                {
                    var existing = owned as LegalDocumentForm;
                    if (existing == null || existing.IsDisposed) continue;
                    existing.tabs.SelectedIndex = Math.Max(0, Math.Min(selectedDocument, Documents.Length - 1));
                    existing.Show();
                    existing.Activate();
                    return;
                }
            // Keep the main Stop controls usable while documents are open.
            var form = new LegalDocumentForm(selectedDocument);
            try { form.Show(owner); }
            catch { form.Dispose(); throw; }
        }

        internal static LinkLabel CreateLink(string key, int document)
        {
            var link = UiText.Bind(new LinkLabel { Name = key, AutoSize = true, BackColor = Color.Transparent,
                LinkColor = Color.FromArgb(102, 86, 213), TabStop = true, Margin = new Padding(0, 4, 16, 8) }, key);
            link.LinkClicked += (s, e) => ShowDocument(link.FindForm(), document);
            return link;
        }
    }
}
