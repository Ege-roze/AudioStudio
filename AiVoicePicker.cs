using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ses_uyg
{
    internal sealed class AiVoicePicker : GlassLayoutPanel
    {
        private readonly ComboBox voices = new ComboBox { Name = "aiVoiceSelector", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 8) };
        private readonly Label details = new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent };
        private readonly Label hint = new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 8) };
        private readonly Button download = UiText.Bind(new GlassButton { AutoSize = true, Name = "aiVoiceDownload" }, "AiVoiceDownload");
        private readonly Button remove = UiText.Bind(new GlassButton { AutoSize = true, Name = "aiVoiceRemove" }, "AiVoiceRemove");
        private readonly Label storage = new Label { Name = "aiVoiceStorage", AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 6) };
        private readonly LinkLabel terms = new LinkLabel { AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 7, 0, 8) };
        private bool available = true;
        internal event EventHandler SelectionChanged;
        internal event EventHandler DownloadRequested;
        internal event EventHandler RemoveRequested;
        internal AiVoiceDefinition SelectedVoice { get { return voices.SelectedItem as AiVoiceDefinition ?? AiVoiceCatalog.All[0]; } set { voices.SelectedItem = value; } }
        internal AiVoiceStore Store { get; private set; }
        internal AiVoicePicker(AiVoiceStore store)
        {
            Store = store;
            Name = "aiVoicePicker";
            ColumnCount = 1; AutoSize = true; Dock = DockStyle.Top;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Margin = new Padding(0, 0, 0, 8);
            Controls.Add(UiText.Bind(new Label { AutoSize = true, BackColor = Color.Transparent }, "AiVoiceSelect"));
            voices.Items.AddRange(AiVoiceCatalog.All.Cast<object>().ToArray());
            voices.SelectedIndex = 0;
            Controls.Add(voices); Controls.Add(details); Controls.Add(hint);
            download.BackColor = Color.FromArgb(226, 230, 249); download.ForeColor = Color.FromArgb(83, 64, 170);
            remove.BackColor = Color.FromArgb(250, 230, 232); remove.ForeColor = Color.FromArgb(158, 54, 69);
            Controls.Add(UiText.Bind(new Label { AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 6, 0, 0) }, "AiVoiceManagement"));
            Controls.Add(storage);
            var actions = new GlassFlowPanel { AutoSize = true, Dock = DockStyle.Top, Margin = Padding.Empty, WrapContents = true };
            foreach (var button in new[] { download, remove })
            {
                button.MinimumSize = new Size(120, 32);
                button.Padding = new Padding(8, 3, 8, 3);
                button.Margin = new Padding(0, 0, 8, 4);
                actions.Controls.Add(button);
            }
            Controls.Add(actions); Controls.Add(terms);
            voices.SelectedIndexChanged += (s, e) => { RefreshStatus(); if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); };
            download.Click += (s, e) => { if (DownloadRequested != null) DownloadRequested(this, EventArgs.Empty); };
            remove.Click += (s, e) => { if (RemoveRequested != null) RemoveRequested(this, EventArgs.Empty); };
            terms.LinkClicked += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SelectedVoice.TermsUrl) { UseShellExecute = true }); }
                catch (Exception ex) { UiText.Set(details, "AiError", ex); }
            };
            AiVoiceStore.Changed += StoreChanged;
            RefreshStatus();
        }
        internal void RefreshStatus()
        {
            var voice = SelectedVoice;
            bool installed = Store.IsInstalled(voice);
            UiText.Set(details, installed ? "AiVoiceInstalled" : "AiVoiceNotInstalled", voice.Name, voice.Bytes / 1000000m);
            UiText.Set(hint, voice.BufferMilliseconds > 1000 ? "AiVoiceLongBuffer" : voice.HasPitch ? "AiVoiceSharedEngine" : "AiVoiceNoPitch");
            UiText.Set(terms, "AiVoiceCredit", voice.Credit);
            UiText.Set(download, installed ? "AiVoiceDownloaded" : "AiVoiceDownload");
            try
            {
                var usage = Store.GetStorage();
                UiText.Set(storage, usage.PartialBytes > 0 ? "AiVoiceStoragePartial" : "AiVoiceStorage",
                    usage.InstalledCount, AiVoiceCatalog.All.Count, usage.TotalBytes / 1000000m, usage.PartialBytes / 1000000m);
            }
            catch (System.IO.IOException) { UiText.Set(storage, "AiVoiceStorageUnavailable"); }
            catch (UnauthorizedAccessException) { UiText.Set(storage, "AiVoiceStorageUnavailable"); }
            catch (InvalidOperationException) { UiText.Set(storage, "AiVoiceStorageUnavailable"); }
            download.Enabled = available && !installed;
            remove.Enabled = available && Store.HasLocalFiles(voice);
        }
        internal void SetAvailable(bool available)
        {
            this.available = available;
            voices.Enabled = available;
            download.Enabled = available && !Store.IsInstalled(SelectedVoice);
            remove.Enabled = available && Store.HasLocalFiles(SelectedVoice);
        }
        internal bool ConfirmRemoval(IWin32Window owner)
        {
            return MessageBox.Show(owner, UiText.Get("AiVoiceRemoveConfirm", SelectedVoice.Name), UiText.Get("AiVoiceManagement"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        private void StoreChanged(string directory)
        {
            if (!String.Equals(directory, Store.DirectoryPath, StringComparison.OrdinalIgnoreCase) || !IsHandleCreated || IsDisposed || Disposing) return;
            try
            {
                if (InvokeRequired) BeginInvoke(new Action(() => { if (!IsDisposed && !Disposing) RefreshStatus(); }));
                else RefreshStatus();
            }
            catch (InvalidOperationException) { } // The window may close while a download finishes.
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RefreshStatus();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) AiVoiceStore.Changed -= StoreChanged;
            base.Dispose(disposing);
        }
    }
}
