using System;
using System.Drawing;
using System.Windows.Forms;
using NAudio.Wave;

namespace ses_uyg
{
    internal sealed partial class AudioTestForm : Form
    {
        private sealed class DeviceItem
        {
            public int Number;
            public string Name;
            public override string ToString() { return Name; }
        }

        private readonly Func<bool> ensureAudioRunning;
        private readonly ComboBox source = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox destination = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Button start = UiText.Bind(new GlassButton { AutoSize = true }, "TestStart");
        private readonly Button stop = UiText.Bind(new GlassButton { AutoSize = true, Enabled = false }, "TestStop");
        private readonly Button refresh = UiText.Bind(new GlassButton { AutoSize = true }, "Refresh");
        private readonly Label status = UiText.Bind(new Label { AutoSize = true, MaximumSize = new Size(530, 0) }, "TestSelectDevices");
        private readonly Label volumeLabel = UiText.Bind(new Label { AutoSize = true }, "TestVolume", 30);
        private readonly TrackBar volume = new TrackBar { Minimum = 0, Maximum = 100, Value = 30, TickFrequency = 10, Dock = DockStyle.Fill };
        private WaveInEvent capture;
        private WaveOutEvent playback;
        private VolumeWaveProvider16 monitorVolume;

        internal AudioTestForm(Func<bool> ensureAudioRunning, Func<AiPreviewLease> acquireAiPreview)
        {
            this.ensureAudioRunning = ensureAudioRunning;
            this.acquireAiPreview = acquireAiPreview;
            UiText.Set(this, "TestTitle");
            SuspendLayout();
            DoubleBuffered = true;
            ResizeRedraw = true;
            Font = new Font("Segoe UI", 10f);
            ForeColor = Color.FromArgb(38, 48, 77);
            BackColor = Color.FromArgb(230, 237, 250);
            ClientSize = new Size(700, 820);
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.Sizable;
            SizeGripStyle = SizeGripStyle.Show;
            MinimumSize = new Size(560, 500);
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterParent;
            AutoScroll = true;

            var layout = new GlassLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(24), ColumnCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);
            layout.LocationChanged += (s, e) => Invalidate(true);
            var header = GlassPanel.CreateHeader();
            layout.Controls.Add(header);
            header.Controls.Add(UiText.Bind(new Label { AutoSize = true,
                BackColor = Color.Transparent, ForeColor = Color.FromArgb(102, 86, 213),
                Font = new Font("Segoe UI Semibold", 8f), Margin = new Padding(0, 0, 0, 6) }, "Brand"));
            header.Controls.Add(UiText.Bind(new Label { AutoSize = true,
                BackColor = Color.Transparent, Font = new Font("Segoe UI Semibold", 25f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8) }, "TestHeading"));
            header.Controls.Add(TestCaption("TestDescription"));
            layout.Controls.Add(CreateAiTestCard());

            var devices = TestCard("TestDevices");
            devices.Controls.Add(TestCaption("TestSource"));
            devices.Controls.Add(source);
            devices.Controls.Add(TestCaption("TestDestination"));
            devices.Controls.Add(destination);
            devices.Controls.Add(TestCaption("HeadphonesHint"));
            layout.Controls.Add(devices);
            foreach (var selector in new[] { source, destination })
            {
                selector.FlatStyle = FlatStyle.Flat;
                selector.BackColor = Color.FromArgb(242, 245, 253);
                selector.ForeColor = ForeColor;
                selector.Margin = new Padding(0, 4, 0, 12);
            }

            var listening = TestCard("TestCard");
            volumeLabel.BackColor = Color.Transparent;
            volumeLabel.Margin = new Padding(0, 4, 0, 4);
            listening.Controls.Add(volumeLabel);
            volume.AutoSize = false;
            volume.Height = 34;
            volume.TickStyle = TickStyle.None;
            volume.BackColor = Color.FromArgb(242, 245, 253);
            volume.Margin = new Padding(0, 0, 0, 12);
            listening.Controls.Add(volume);
            StyleTestButton(start, Color.FromArgb(102, 86, 213), Color.White);
            StyleTestButton(stop, Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
            StyleTestButton(refresh, Color.FromArgb(226, 230, 249), Color.FromArgb(102, 86, 213));
            var buttons = new GlassFlowPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
            buttons.Controls.AddRange(new Control[] { start, stop, refresh });
            listening.Controls.Add(buttons);
            status.BackColor = Color.Transparent;
            status.MaximumSize = Size.Empty;
            status.Dock = DockStyle.Top;
            status.Margin = new Padding(0, 4, 0, 4);
            listening.Controls.Add(status);
            layout.Controls.Add(listening);
            layout.Controls.Add(TestCaption("TestDiscordHint"));

            start.Click += (s, e) => StartTest();
            stop.Click += (s, e) => StopTest();
            refresh.Click += (s, e) => RefreshDevices();
            volume.ValueChanged += (s, e) =>
            {
                UiText.Set(volumeLabel, "TestVolume", volume.Value);
                if (monitorVolume != null) monitorVolume.Volume = volume.Value / 100f;
            };
            RefreshDevices();
            ResumeLayout(true);
            ApplicationTheme.Apply(this);
        }

        private static Label TestCaption(string key)
        {
            return UiText.Bind(new Label { AutoSize = true, Dock = DockStyle.Top,
                BackColor = Color.Transparent, ForeColor = Color.FromArgb(65, 86, 115),
                Margin = new Padding(0, 3, 0, 5) }, key);
        }

        private static GlassPanel TestCard(string title)
        {
            var card = new GlassPanel { Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1,
                Padding = new Padding(22, 20, 22, 22), Margin = new Padding(0, 14, 0, 0) };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Controls.Add(UiText.Bind(new Label { AutoSize = true, BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) }, title));
            return card;
        }

        private static void StyleTestButton(Button button, Color background, Color foreground)
        {
            button.MinimumSize = new Size(80, 38);
            button.Padding = new Padding(8, 3, 8, 3);
            button.Margin = new Padding(0, 0, 8, 4);
            button.BackColor = background;
            button.ForeColor = foreground;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
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
            Invalidate(true);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate(true);
        }


        private static bool IsCable(string name)
        {
            return name.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshDevices()
        {
            if (capture != null || aiTestCancellation != null) return;
            source.Items.Clear();
            destination.Items.Clear();
            aiTestMicrophone.Items.Clear();
            aiTestDestination.Items.Clear();
            try
            {
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    string name = WaveIn.GetCapabilities(i).ProductName;
                    if (IsCable(name) && name.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0)
                        source.Items.Add(new DeviceItem { Number = i, Name = name });
                    if (!IsCable(name)) aiTestMicrophone.Items.Add(new DeviceItem { Number = i, Name = name });
                }
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    string name = WaveOut.GetCapabilities(i).ProductName;
                    // Testi kabloya geri göndermek bir ses döngüsü oluşturur.
                    if (!IsCable(name))
                    {
                        destination.Items.Add(new DeviceItem { Number = i, Name = name });
                        aiTestDestination.Items.Add(new DeviceItem { Number = i, Name = name });
                    }
                }
                if (source.Items.Count == 1) source.SelectedIndex = 0;
                if (aiTestMicrophone.Items.Count == 1) aiTestMicrophone.SelectedIndex = 0;
                if (destination.Items.Count == 1) destination.SelectedIndex = 0;
                if (aiTestDestination.Items.Count == 1) aiTestDestination.SelectedIndex = 0;
                UiText.Set(status, source.Items.Count == 0 ? "TestCableMissing"
                    : destination.Items.Count == 0 ? "TestDestinationMissing" : "TestReady");
            }
            catch (Exception ex) { UiText.Set(status, "DevicesError", ex); }
        }

        private void StartTest()
        {
            if (capture != null || aiTestCancellation != null) return;
            var input = source.SelectedItem as DeviceItem;
            var output = destination.SelectedItem as DeviceItem;
            if (input == null || output == null)
            {
                UiText.Set(status, "TestSelectDevices");
                return;
            }
            try
            {
                if (input.Number >= WaveIn.DeviceCount || output.Number >= WaveOut.DeviceCount
                    || WaveIn.GetCapabilities(input.Number).ProductName != input.Name
                    || WaveOut.GetCapabilities(output.Number).ProductName != output.Name)
                    throw UiText.Error("DevicesChanged");
                if (!ensureAudioRunning())
                {
                    UiText.Set(status, "TestMainStopped");
                    return;
                }

                var session = new WaveInEvent { DeviceNumber = input.Number, WaveFormat = new WaveFormat(44100, 1), BufferMilliseconds = 30 };
                capture = session;
                var buffer = new BufferedWaveProvider(session.WaveFormat) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(1) };
                monitorVolume = new VolumeWaveProvider16(buffer) { Volume = volume.Value / 100f };
                playback = new WaveOutEvent { DeviceNumber = output.Number, DesiredLatency = 100 };
                session.DataAvailable += (s, e) => buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                session.RecordingStopped += (s, e) => SessionStopped(session, e.Exception);
                playback.PlaybackStopped += (s, e) => SessionStopped(session, e.Exception);
                playback.Init(monitorVolume);
                playback.Play();
                session.StartRecording();
                SetRunning(true);
                UiText.Set(status, "TestRunning", input.Name, output.Name);
            }
            catch (Exception ex)
            {
                StopTest();
                UiText.Set(status, "TestStartError", ex);
            }
        }

        private void SessionStopped(WaveInEvent session, Exception error)
        {
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (capture != session || IsDisposed) return;
                    StopTest();
                    UiText.Set(status, error == null ? "TestEnded" : "TestStoppedError", error);
                }));
            }
            catch (InvalidOperationException) { } // Pencere kapanırken bekleyen bildirim.
        }

        internal void StopTest()
        {
            // Önce oturumu ayır: gecikmiş olaylar yeni testi durduramasın.
            var oldCapture = capture;
            var oldPlayback = playback;
            capture = null;
            playback = null;
            monitorVolume = null;
            Exception cleanupError = null;
            try { if (oldCapture != null) oldCapture.Dispose(); }
            catch (Exception ex) { cleanupError = ex; }
            try { if (oldPlayback != null) oldPlayback.Dispose(); }
            catch (Exception ex) { cleanupError = ex; }
            SetRunning(false);
            UiText.Set(status, cleanupError == null ? "TestStopped" : "TestCleanupError", cleanupError);
        }

        private void SetRunning(bool running)
        {
            start.Enabled = !running && aiTestCancellation == null;
            stop.Enabled = running;
            source.Enabled = destination.Enabled = refresh.Enabled = !running && aiTestCancellation == null;
            UpdateAiTestControls();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAiTest();
                StopTest();
                aiTestMeterTimer.Stop();
                if (aiTestCancellation == null) aiTestMeterTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
