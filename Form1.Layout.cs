using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ses_uyg
{
    public partial class Form1
    {
        private Label soundCount;
        private Label selectedSoundPath;
        private Button playSound;
        private ComboBox languageSelector;
        private Label soundStatus;
        private Label libraryStatus;
        private Control aiVoiceSection;
        private Control effectsSection;
        private Control soundLibrarySection;
        private Label audioLockedHint;
        private AboutForm aboutForm;
        private PersonalizationForm personalizationForm;
        private static readonly Color Surface = Color.FromArgb(242, 245, 253);
        private static readonly Color Ink = Color.FromArgb(38, 48, 77);
        private static readonly Color Accent = Color.FromArgb(102, 86, 213);

        private void InitializeMainLayout()
        {
            SuspendLayout();
            DoubleBuffered = true;
            ResizeRedraw = true;
            var testButton = testAudioButton;
            var licenseLink = Controls.OfType<LinkLabel>().First();
            var oldControls = Controls.Cast<Control>().ToArray();
            Controls.Clear();
            UiText.Set(this, "AppTitle");
            Font = new Font("Segoe UI", 10f);
            ForeColor = Ink;
            BackColor = Color.FromArgb(230, 237, 250);
            AutoSize = false;
            AutoSizeMode = AutoSizeMode.GrowOnly;
            FormBorderStyle = FormBorderStyle.Sizable;
            SizeGripStyle = SizeGripStyle.Show;
            ClientSize = new Size(1100, 860);
            MinimumSize = new Size(940, 650);
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScroll = true;

            var page = new GlassLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(24), BackColor = Color.Transparent };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(page);
            // Also covers focus-driven and programmatic AutoScrollPosition changes.
            page.LocationChanged += (s, e) => Invalidate(true);
            var header = new GlassPanel { IsHeader = true, Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3, Padding = new Padding(16, 14, 16, 16), Margin = new Padding(0, 0, 0, 18) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 480));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            var titles = Stack();
            titles.Controls.Add(UiText.Bind(new Label { AutoSize = true, ForeColor = Accent, BackColor = Color.Transparent, Font = new Font("Segoe UI Semibold", 8f), Margin = new Padding(0, 0, 0, 6) }, "Brand"));
            titles.Controls.Add(Heading("AppTitle", 25));
            titles.Controls.Add(Caption("Tagline"));
            header.Controls.Add(titles, 0, 0);
            UiText.Set(btnStart, "Start");
            UiText.Set(btnStop, "Stop");
            StyleButton(btnStart, Accent, Color.White);
            StyleButton(btnStop, Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
            StyleButton(testButton, Ink, Color.White);
            var exit = UiText.Bind(new GlassButton { Name = "exitApplicationButton" }, "ExitApplication");
            StyleButton(exit, Ink, Color.White);
            exit.Click += (s, e) => ExitApplication();
            var transport = ButtonRow(btnStart, btnStop, exit);
            transport.WrapContents = false;
            var actions = Stack();
            actions.Controls.Add(transport);
            languageSelector = new ComboBox
            {
                Name = "languageSelector", DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150, FlatStyle = FlatStyle.Flat, BackColor = Surface,
                ForeColor = Ink, Margin = new Padding(8, 0, 0, 0)
            };
            languageSelector.Items.AddRange(new object[] { "Türkçe", "English" });
            languageSelector.SelectedIndex = UiText.Language == UiLanguage.English ? 1 : 0;
            languageSelector.SelectedIndexChanged += LanguageSelector_SelectedIndexChanged;
            var personalize = UiText.Bind(new GlassButton { Name = "personalizeButton" }, "Personalize");
            StyleButton(personalize);
            personalize.Click += (s, e) =>
            {
                if (personalizationForm == null || personalizationForm.IsDisposed)
                {
                    personalizationForm = new PersonalizationForm();
                    personalizationForm.Show(this);
                }
                else personalizationForm.Activate();
            };
            actions.Controls.Add(ButtonRow(Caption("Language"), languageSelector, personalize, testButton));
            actions.Controls.Add(ButtonRow(LegalDocumentForm.CreateLink("PrivacyPolicy", 0),
                LegalDocumentForm.CreateLink("TermsOfService", 1)));
            header.Controls.Add(actions, 1, 0);
            settingsButton = new GlassButton { Name = "settingsButton", Text = "⚙", Font = new Font("Segoe UI Symbol", 19f),
                Size = new Size(44, 40), Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = new Padding(4, 3, 0, 0),
                BackColor = Surface, ForeColor = Accent, Cursor = Cursors.Hand };
            if (components == null) components = new System.ComponentModel.Container();
            settingsTip = new ToolTip(components);
            settingsButton.Click += (s, e) => OpenSettings();
            RefreshSettingsLanguage();
            header.Controls.Add(settingsButton, 2, 0);
            page.Controls.Add(header);
            audioLockedHint = Caption("AudioLockedHint");
            audioLockedHint.Name = "audioLockedHint";
            audioLockedHint.ForeColor = Accent;
            audioLockedHint.Margin = new Padding(0, 0, 0, 14);
            page.Controls.Add(audioLockedHint);
            aiVoiceSection = CreateAiCard();
            page.Controls.Add(aiVoiceSection);

            var content = new GlassLayoutPanel { Dock = DockStyle.Top, Height = 520, ColumnCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 18) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var effects = Card("Effects");
            effectsSection = effects;
            effects.Name = "effectsSection";
            effects.Margin = new Padding(0, 0, 8, 0);
            UiText.Set(btnRadioMode, "Radio");
            UiText.Set(btnCaveMode, "Cave");
            UiText.Set(btnAnimeMode, "Mosquito");
            UiText.Set(btnReset, "Reset");
            foreach (var b in new[] { btnRadioMode, btnCaveMode, btnAnimeMode, btnReset }) StyleButton(b);
            effects.Controls.Add(ButtonRow(btnRadioMode, btnCaveMode, btnAnimeMode, btnReset));
            effects.Controls.Add(Slider(lblPitch, trackBarPitch));
            effects.Controls.Add(Slider(lblDogshit, trackBarDogshit));
            effects.Controls.Add(Slider(lblEcho, trackBarEcho));
            effects.Controls.Add(Caption("Equalizer"));
            var equalizer = new GlassLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, BackColor = Color.Transparent };
            foreach (var pair in new[] { Tuple.Create(lblEQBass, trackBarEQBass), Tuple.Create(lblEQMid, trackBarEQMid), Tuple.Create(lblEQTreble, trackBarEQTreble) })
            {
                equalizer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
                equalizer.Controls.Add(Slider(pair.Item1, pair.Item2));
            }
            effects.Controls.Add(equalizer);
            content.Controls.Add(effects, 0, 0);

            var sounds = Card("Library");
            soundLibrarySection = sounds;
            sounds.Name = "soundLibrarySection";
            sounds.Margin = new Padding(8, 0, 0, 0);
            sounds.Controls.Add(Caption("LibraryHint"));
            sounds.Controls.Add(Caption("Search"));
            txtSearch.Font = Font;
            txtSearch.Text = "";
            txtSearch.ForeColor = Ink;
            txtSearch.BorderStyle = BorderStyle.None;
            txtSearch.BackColor = Surface;
            txtSearch.Dock = DockStyle.Fill;
            txtSearch.Margin = new Padding(0, 4, 0, 8);
            var searchWell = new GlassPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 8, 12, 6), Margin = new Padding(0, 2, 0, 5) };
            searchWell.Controls.Add(txtSearch);
            sounds.Controls.Add(searchWell);
            soundCount = Caption("NoSounds");
            sounds.Controls.Add(soundCount);
            listBoxSounds.Dock = DockStyle.Fill;
            listBoxSounds.IntegralHeight = false;
            listBoxSounds.Height = 145;
            listBoxSounds.BorderStyle = BorderStyle.None;
            listBoxSounds.BackColor = Surface;
            listBoxSounds.ForeColor = Ink;
            listBoxSounds.Font = Font;
            listBoxSounds.DrawMode = DrawMode.OwnerDrawFixed;
            listBoxSounds.ItemHeight = 44;
            listBoxSounds.DrawItem += DrawSound;
            listBoxSounds.SelectedIndexChanged += (s, e) => UpdateSoundSelection();
            listBoxSounds.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { ListBoxSounds_DoubleClick(s, e); e.Handled = e.SuppressKeyPress = true; }
            };
            sounds.Controls.Add(listBoxSounds);
            selectedSoundPath = Caption("SelectSound");
            selectedSoundPath.AutoSize = false;
            selectedSoundPath.AutoEllipsis = true;
            selectedSoundPath.Height = 24;
            selectedSoundPath.Dock = DockStyle.Fill;
            sounds.Controls.Add(selectedSoundPath);
            playSound = UiText.Bind(new GlassButton(), "PlaySound");
            playSound.Click += ListBoxSounds_DoubleClick;
            StyleButton(playSound, Accent, Color.White);
            StyleButton(btnAddSound);
            UiText.Set(btnAddSound, "AddSound");
            UiText.Set(btnDeleteSound, "RemoveSound");
            StyleButton(btnDeleteSound);
            sounds.Controls.Add(ButtonRow(playSound, btnAddSound, btnDeleteSound));
            sounds.Controls.Add(Caption("RemoveHint"));
            soundStatus = new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent, Visible = false, Margin = new Padding(0, 6, 0, 0) };
            libraryStatus = new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent, ForeColor = Color.FromArgb(153, 27, 27), Visible = false, Margin = new Padding(0, 6, 0, 0) };
            sounds.Controls.Add(soundStatus);
            sounds.Controls.Add(libraryStatus);
            content.Controls.Add(sounds, 1, 0);
            content.Layout += (s, e) =>
            {
                int height = Math.Max(effects.Height, sounds.Height);
                if (height > 0 && content.Height != height) content.Height = height;
            };
            page.Controls.Add(content);

            var routing = Card("Routing");
            routing.Margin = Padding.Empty;
            var devices = new GlassLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, BackColor = Color.Transparent };
            devices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            devices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            devices.Controls.Add(Caption("Microphone"), 0, 0);
            devices.Controls.Add(Caption("CableInput"), 1, 0);
            microphoneSelector.Dock = cableSelector.Dock = DockStyle.Fill;
            microphoneSelector.FlatStyle = cableSelector.FlatStyle = FlatStyle.Flat;
            microphoneSelector.BackColor = cableSelector.BackColor = Surface;
            microphoneSelector.ForeColor = cableSelector.ForeColor = Ink;
            devices.Controls.Add(microphoneSelector, 0, 1);
            devices.Controls.Add(cableSelector, 1, 1);
            routing.Controls.Add(devices);
            StyleButton(refreshDevices);
            StyleButton(installCable);
            StyleButton(uninstallCable, Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
            licenseLink.AutoSize = true;
            licenseLink.Margin = new Padding(12, 12, 0, 0);
            licenseLink.LinkColor = Accent;
            routing.Controls.Add(ButtonRow(refreshDevices, installCable, uninstallCable, licenseLink));
            routingStatus.BackColor = Color.Transparent;
            routingStatus.AutoSize = true;
            routingStatus.Dock = DockStyle.Top;
            routingStatus.Margin = new Padding(3, 6, 3, 4);
            routing.Controls.Add(routingStatus);
            routing.Controls.Add(Caption("DiscordHint"));
            discordInputDevices.Dock = DockStyle.Fill;
            discordInputDevices.Height = 42;
            discordInputDevices.BackColor = Surface;
            discordInputDevices.BorderStyle = BorderStyle.None;
            routing.Controls.Add(discordInputDevices);
            page.Controls.Add(routing);
            var moreInfo = UiText.Bind(new LinkLabel
            {
                Name = "moreInfoLink", AutoSize = true, Anchor = AnchorStyles.Right,
                BackColor = Color.Transparent, LinkColor = Accent, ActiveLinkColor = Ink,
                LinkBehavior = LinkBehavior.HoverUnderline, Font = new Font("Segoe UI", 9f),
                Margin = new Padding(3, 18, 3, 0), TabStop = true
            }, "MoreInfo");
            moreInfo.LinkClicked += MoreInfo_LinkClicked;
            page.Controls.Add(moreInfo);
            foreach (var unused in oldControls) if (unused.Parent == null) unused.Dispose();
            ResumeLayout(true);
        }

        private void LanguageSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (languageSelector.SelectedIndex < 0) return;
            UiText.Language = languageSelector.SelectedIndex == 1 ? UiLanguage.English : UiLanguage.Turkish;
            UiText.Apply(this);
            UpdateTrayText();
            RefreshSettingsLanguage();
            if (audioTestForm != null && !audioTestForm.IsDisposed) UiText.Apply(audioTestForm);
            if (aboutForm != null && !aboutForm.IsDisposed) UiText.Apply(aboutForm);
            if (personalizationForm != null && !personalizationForm.IsDisposed) personalizationForm.RefreshLanguage();
        }

        private void MoreInfo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (aboutForm == null || aboutForm.IsDisposed)
            {
                aboutForm = new AboutForm();
                aboutForm.Show(this);
            }
            else aboutForm.Activate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            GlassDrawing.Backdrop(e.Graphics, this);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate(true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Fixed-size labels also need the newly sized cached backdrop.
            Invalidate(true);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            // Native scrolling reuses old pixels; glass depends on its new
            // position over the backdrop, so every child needs a fresh paint.
            Invalidate(true);
        }


        private static TableLayoutPanel Stack()
        {
            var panel = new GlassLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return panel;
        }

        private static TableLayoutPanel Card(string title)
        {
            var card = new GlassPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Margin = Padding.Empty };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Padding = new Padding(22, 20, 22, 22);
            card.Controls.Add(Heading(title, 14));
            return card;
        }

        private static Label Heading(string key, float size)
        {
            return UiText.Bind(new Label { AutoSize = true, BackColor = Color.Transparent, Font = new Font("Segoe UI Semibold", size, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) }, key);
        }

        private static Label Caption(string key)
        {
            return UiText.Bind(new Label { AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent, ForeColor = Color.FromArgb(65, 86, 115), Margin = new Padding(0, 3, 0, 5) }, key);
        }

        private static FlowLayoutPanel ButtonRow(params Control[] buttons)
        {
            var row = new GlassFlowPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0, 3, 0, 6), WrapContents = true };
            row.Controls.AddRange(buttons);
            return row;
        }

        private static void StyleButton(Button button, Color? background = null, Color? foreground = null)
        {
            button.AutoSize = true;
            button.Size = new Size(88, 38);
            button.MinimumSize = new Size(80, 38);
            button.Padding = new Padding(8, 3, 8, 3);
            button.Margin = new Padding(0, 0, 8, 4);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = background ?? Color.FromArgb(226, 230, 249);
            button.ForeColor = foreground ?? Accent;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        private static TableLayoutPanel Slider(Label label, TrackBar slider)
        {
            var panel = Stack();
            label.BackColor = Color.Transparent;
            label.Font = new Font("Segoe UI", 9f);
            label.Margin = new Padding(0, 5, 0, 0);
            slider.Dock = DockStyle.Top;
            slider.AutoSize = false;
            slider.Height = 34;
            slider.TickStyle = TickStyle.None;
            slider.BackColor = Surface;
            slider.Margin = Padding.Empty;
            panel.Controls.Add(label);
            panel.Controls.Add(slider);
            return panel;
        }

        private void UpdateSoundSelection()
        {
            if (soundCount == null) return;
            var selected = listBoxSounds.SelectedItem as SoundItem;
            UiText.Set(soundCount, soundList.Count == 0 ? "NoSounds"
                : listBoxSounds.Items.Count == 0 ? "NoMatches" : "SoundCount", listBoxSounds.Items.Count, soundList.Count);
            playSound.Enabled = selected != null;
            btnDeleteSound.Enabled = selected != null && !libraryLoadFailed;
            if (selected == null) UiText.Set(selectedSoundPath, "SelectSound");
            else UiText.SetLiteral(selectedSoundPath, selected.Path);
        }

        private void DrawSound(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var background = new SolidBrush(selected ? (ApplicationTheme.Classic ? ApplicationTheme.Blue : Color.FromArgb(224, 231, 255))
                : ApplicationTheme.Classic && e.Index % 2 == 1 ? Color.FromArgb(237, 243, 249) : listBoxSounds.BackColor))
                e.Graphics.FillRectangle(background, e.Bounds);
            var sound = (SoundItem)listBoxSounds.Items[e.Index];
            var bounds = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 24), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, sound.Name, listBoxSounds.Font, bounds,
                ApplicationTheme.Classic ? (selected ? Color.White : ApplicationTheme.Ink) : selected ? Accent : Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }
    }
}
