using System;
using System.Drawing;
using System.Windows.Forms;

namespace ses_uyg
{
    internal sealed class SettingsForm : Form
    {
        private readonly Form1 main;
        private readonly ComboBox microphone = Selector("settingsMicrophone");
        private readonly ComboBox output = Selector("settingsOutput");
        private readonly CheckBox closeToTray = Choice("SettingsCloseToTray", "closeToTray");
        private readonly CheckBox startInTray = Choice("SettingsStartInTray", "startInTray");
        private readonly CheckBox startWithWindows = Choice("SettingsStartWithWindows", "startWithWindows");
        private readonly Label deviceHint = TextBlock("SettingsDeviceHint");
        private readonly Label status = TextBlock("SettingsSaveHint");
        private readonly Button refresh = Button("Refresh");
        private readonly Button save = Button("AppearanceSave");
        private bool? devicesAvailable;
        private bool initialCloseToTray;
        private bool initialStartInTray;
        private bool initialStartWithWindows;
        private object initialMicrophone;
        private object initialOutput;
        private bool loadingDevices;
        private bool settingsLoaded;

        internal SettingsForm(Form1 main)
        {
            this.main = main;
            Name = "settingsForm";
            UiText.Set(this, "SettingsTitle");
            Icon = main.Icon;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Font = new Font("Segoe UI", 10f);
            ForeColor = Color.FromArgb(38, 48, 77);
            BackColor = Color.FromArgb(230, 237, 250);
            ClientSize = new Size(700, 820);
            MinimumSize = new Size(580, 500);
            FormBorderStyle = FormBorderStyle.Sizable;
            SizeGripStyle = SizeGripStyle.Show;
            StartPosition = FormStartPosition.CenterParent;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };
            Controls.Add(scroll);
            scroll.Scroll += (s, e) => Invalidate(true);
            var page = Stack();
            page.Padding = new Padding(24);
            scroll.Controls.Add(page);
            page.LocationChanged += (s, e) => Invalidate(true);
            var header = GlassPanel.CreateHeader();
            page.Controls.Add(header);
            var heading = TextBlock("SettingsTitle");
            heading.Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold);
            header.Controls.Add(heading);
            header.Controls.Add(TextBlock("SettingsDescription"));

            var audio = Card("SettingsAudio");
            audio.Controls.Add(TextBlock("Microphone"));
            audio.Controls.Add(microphone);
            audio.Controls.Add(TextBlock("CableInput"));
            audio.Controls.Add(output);
            audio.Controls.Add(deviceHint);
            audio.Controls.Add(Row(refresh));
            refresh.Click += (s, e) => main.RefreshSettingsDevices();
            page.Controls.Add(audio);

            var behavior = Card("SettingsBehavior");
            behavior.Controls.Add(closeToTray);
            behavior.Controls.Add(TextBlock("SettingsCloseHint"));
            behavior.Controls.Add(startInTray);
            behavior.Controls.Add(startWithWindows);
            behavior.Controls.Add(TextBlock("SettingsStartupHint"));
            page.Controls.Add(behavior);

            var legal = Card("LegalDocuments");
            legal.Controls.Add(TextBlock("PrivacyShort"));
            legal.Controls.Add(LegalDocumentForm.CreateLink("PrivacyPolicy", 0));
            legal.Controls.Add(LegalDocumentForm.CreateLink("TermsOfService", 1));
            legal.Controls.Add(LegalDocumentForm.CreateLink("ThirdPartyNotices", 2));
            page.Controls.Add(legal);

            var footer = new GlassPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Bottom, ColumnCount = 1, Padding = new Padding(24, 12, 24, 14) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.Controls.Add(status);
            var cancel = Button("AppearanceCancel");
            save.Name = "saveSettings";
            save.Visible = false;
            save.BackColor = Color.FromArgb(102, 86, 213);
            save.ForeColor = Color.White;
            footer.Controls.Add(Row(cancel, save));
            Controls.Add(footer);
            CancelButton = cancel;
            cancel.Click += (s, e) => Close();
            save.Click += (s, e) => SaveSettings();

            var options = main.ApplicationSettings;
            closeToTray.Checked = options.CloseToTray;
            startInTray.Checked = options.StartInTray;
            try { startWithWindows.Checked = main.StartsWithWindows; }
            catch (Exception ex)
            {
                startWithWindows.Enabled = false;
                status.ForeColor = Color.Firebrick;
                UiText.Set(status, "SettingsStartupError", ex);
            }
            ReloadDevices();
            initialCloseToTray = closeToTray.Checked;
            initialStartInTray = startInTray.Checked;
            initialStartWithWindows = startWithWindows.Checked;
            settingsLoaded = true;
            closeToTray.CheckedChanged += (s, e) => UpdateSaveState();
            startInTray.CheckedChanged += (s, e) => UpdateSaveState();
            startWithWindows.CheckedChanged += (s, e) => UpdateSaveState();
            startWithWindows.EnabledChanged += (s, e) => UpdateSaveState();
            microphone.SelectedIndexChanged += (s, e) => UpdateSaveState();
            output.SelectedIndexChanged += (s, e) => UpdateSaveState();
            UpdateSaveState();
            ApplicationTheme.Apply(this);
        }

        internal void ReloadDevices()
        {
            LoadDeviceChoices();
            devicesAvailable = main.CanChangeAudioDevices;
            UpdateDeviceAvailability();
        }

        internal void UpdateDeviceAvailability()
        {
            bool available = main.CanChangeAudioDevices;
            if (devicesAvailable != available)
            {
                // If a session starts while editing, show the actual active devices.
                LoadDeviceChoices();
                devicesAvailable = available;
            }
            microphone.Enabled = output.Enabled = refresh.Enabled = available;
            UiText.Set(deviceHint, !available ? "SettingsDevicesLocked" :
                (microphone.Items.Count == 0 || output.Items.Count == 0) ? "SettingsDevicesMissing" : "SettingsDeviceHint");
            UpdateSaveState();
        }

        internal void RefreshLanguage() { UiText.Apply(this); UpdateDeviceAvailability(); }

        private void LoadDeviceChoices()
        {
            loadingDevices = true;
            try
            {
                main.CopyDeviceChoices(microphone, output);
                // Refreshing devices or starting a stream reloads the actual choices, not a user edit.
                initialMicrophone = microphone.SelectedItem;
                initialOutput = output.SelectedItem;
            }
            finally { loadingDevices = false; }
        }

        private bool HasChanges
        {
            get
            {
                return settingsLoaded && (closeToTray.Checked != initialCloseToTray
                    || startInTray.Checked != initialStartInTray
                    || (startWithWindows.Enabled && startWithWindows.Checked != initialStartWithWindows)
                    || (main.CanChangeAudioDevices && (!Equals(microphone.SelectedItem, initialMicrophone)
                        || !Equals(output.SelectedItem, initialOutput))));
            }
        }

        private void UpdateSaveState()
        {
            if (loadingDevices) return;
            bool changed = HasChanges;
            save.Visible = save.Enabled = changed;
            AcceptButton = changed ? save : null;
        }

        private void SaveSettings()
        {
            if (!HasChanges) return;
            try
            {
                main.SaveApplicationSettings(closeToTray.Checked, startInTray.Checked,
                    startWithWindows.Enabled ? (bool?)startWithWindows.Checked : null, microphone.SelectedItem, output.SelectedItem);
                Close();
            }
            catch (Exception ex)
            {
                status.ForeColor = Color.Firebrick;
                UiText.Set(status, "SettingsSaveError", ex);
            }
        }

        private static ComboBox Selector(string name)
        {
            return new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(242, 245, 253), Margin = new Padding(0, 3, 0, 10) };
        }
        private static CheckBox Choice(string key, string name)
        {
            return UiText.Bind(new CheckBox { Name = name, Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent,
                Margin = new Padding(0, 3, 0, 3), Padding = new Padding(2) }, key);
        }
        private static Label TextBlock(string key)
        {
            return UiText.Bind(new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent,
                Margin = new Padding(0, 3, 0, 8) }, key);
        }
        private static Button Button(string key)
        {
            return UiText.Bind(new GlassButton { AutoSize = true, MinimumSize = new Size(80, 36), Padding = new Padding(9, 3, 9, 3),
                Margin = new Padding(0, 0, 8, 0), BackColor = Color.FromArgb(226, 230, 249),
                ForeColor = Color.FromArgb(102, 86, 213), Cursor = Cursors.Hand }, key);
        }
        private static GlassLayoutPanel Stack()
        {
            var panel = new GlassLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, ColumnCount = 1, Margin = Padding.Empty };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return panel;
        }
        private static GlassPanel Card(string key)
        {
            var panel = new GlassPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, ColumnCount = 1, Padding = new Padding(20), Margin = new Padding(0, 12, 0, 0) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var title = TextBlock(key);
            title.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
            panel.Controls.Add(title);
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
    }
}
