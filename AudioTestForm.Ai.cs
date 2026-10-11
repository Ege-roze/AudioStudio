using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace ses_uyg
{
    // The test borrows the main engine exclusively; it never starts the main audio route.
    internal sealed class AiPreviewLease : IDisposable
    {
        internal readonly AiRuntime Runtime;
        internal readonly int Pitch;
        internal AiVoiceDefinition Voice = AiVoiceCatalog.All[0];
        internal bool Prepared;
        private Action<bool> release;

        internal AiPreviewLease(AiRuntime runtime, int pitch, Action<bool> release)
        {
            Runtime = runtime;
            Pitch = pitch;
            this.release = release;
        }

        public void Dispose()
        {
            var callback = release;
            release = null;
            if (callback != null) callback(Prepared);
        }
    }

    internal sealed partial class AudioTestForm
    {
        private readonly Func<AiPreviewLease> acquireAiPreview;
        private readonly object aiTestSync = new object();
        private readonly ComboBox aiTestMicrophone = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox aiTestDestination = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Button aiTestStart = UiText.Bind(new GlassButton { AutoSize = true }, "AiTestStart");
        private readonly Button aiTestStop = UiText.Bind(new GlassButton { AutoSize = true, Enabled = false }, "AiTestStop");
        private readonly Button aiTestRefresh = UiText.Bind(new GlassButton { AutoSize = true }, "Refresh");
        private readonly Label aiTestStatus = TestCaption("AiTestReady");
        private readonly Label aiTestUnavailable = TestCaption("AiTestUnavailable");
        private readonly Label aiTestVoice = TestCaption("AiFemale");
        private readonly AiVoicePicker aiTestVoicePicker = new AiVoicePicker(AiVoiceStore.Default());
        private readonly Label aiTestVolumeLabel = UiText.Bind(new Label { AutoSize = true, Dock = DockStyle.Top,
            BackColor = Color.Transparent, Margin = new Padding(0, 3, 0, 5) }, "TestVolume", 30);
        private readonly TrackBar aiTestVolume = new TrackBar { Minimum = 0, Maximum = 100, Value = 30, Dock = DockStyle.Fill };
        private readonly ProgressBar aiTestProgress = new ProgressBar { Dock = DockStyle.Top, Height = 18, Visible = false };
        private readonly ProgressBar aiTestInputLevel = new ProgressBar { Dock = DockStyle.Top, Height = 12 };
        private readonly ProgressBar aiTestOutputLevel = new ProgressBar { Dock = DockStyle.Top, Height = 12 };
        private readonly System.Windows.Forms.Timer aiTestMeterTimer = new System.Windows.Forms.Timer { Interval = 200 };
        private int aiTestInputPeak;
        private int aiTestOutputPeak;
        private int aiTestInputBlocks;
        private int aiTestOutputBlocks;
        private long aiTestInputTime;
        private long aiTestOutputTime;
        private bool aiTestAllowed = true;
        private CancellationTokenSource aiTestCancellation;
        private Exception aiTestError;
        private WaveInEvent aiTestCapture;
        private WaveOutEvent aiTestPlayback;
        private AiVoiceSession aiTestSession;
        private VolumeWaveProvider16 aiTestMonitorVolume;

        internal void SelectAiVoice(AiVoiceDefinition voice) { if (aiTestCancellation == null) aiTestVoicePicker.SelectedVoice = voice; }

        private Control CreateAiTestCard()
        {
            var card = TestCard("AiTestTitle");
            card.Name = "aiTestSection";
            card.Controls.Add(TestCaption("AiTestDescription"));
            card.Controls.Add(aiTestVoice);
            card.Controls.Add(aiTestVoicePicker);
            aiTestVoicePicker.DownloadRequested += async (s, e) => await DownloadAiTestVoiceAsync();
            aiTestVoicePicker.RemoveRequested += async (s, e) => await ManageAiTestVoiceAsync(true);
            card.Controls.Add(TestCaption("Microphone"));
            card.Controls.Add(aiTestMicrophone);
            card.Controls.Add(TestCaption("AiTestDestination"));
            card.Controls.Add(aiTestDestination);
            foreach (var selector in new[] { aiTestMicrophone, aiTestDestination })
            {
                selector.FlatStyle = FlatStyle.Flat;
                selector.BackColor = Color.FromArgb(242, 245, 253);
                selector.ForeColor = ForeColor;
                selector.Margin = new Padding(0, 4, 0, 8);
            }
            card.Controls.Add(TestCaption("HeadphonesHint"));
            card.Controls.Add(aiTestVolumeLabel);
            aiTestVolume.AutoSize = false;
            aiTestVolume.Height = 34;
            aiTestVolume.TickStyle = TickStyle.None;
            aiTestVolume.BackColor = Color.FromArgb(242, 245, 253);
            card.Controls.Add(aiTestVolume);
            StyleTestButton(aiTestStart, Color.FromArgb(102, 86, 213), Color.White);
            StyleTestButton(aiTestStop, Color.FromArgb(254, 226, 226), Color.FromArgb(153, 27, 27));
            StyleTestButton(aiTestRefresh, Color.FromArgb(226, 230, 249), Color.FromArgb(102, 86, 213));
            var buttons = new GlassFlowPanel { AutoSize = true, Dock = DockStyle.Top };
            buttons.Controls.AddRange(new Control[] { aiTestStart, aiTestStop, aiTestRefresh });
            card.Controls.Add(buttons);
            var levels = new GlassLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 6, 0, 8) };
            levels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            levels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            levels.Controls.Add(TestCaption("AiTestInputLevel"), 0, 0);
            levels.Controls.Add(TestCaption("AiTestOutputLevel"), 1, 0);
            levels.Controls.Add(aiTestInputLevel, 0, 1);
            levels.Controls.Add(aiTestOutputLevel, 1, 1);
            card.Controls.Add(levels);
            card.Controls.Add(aiTestStatus);
            card.Controls.Add(aiTestProgress);
            card.Controls.Add(aiTestUnavailable);
            card.Controls.Add(TestCaption("AiTestInstallHint"));
            card.Controls.Add(TestCaption("AiNetworkDisclosure"));
            card.Controls.Add(LegalDocumentForm.CreateLink("PrivacyPolicy", 0));
            card.Controls.Add(LegalDocumentForm.CreateLink("ModelTerms", 3));
            aiTestStart.Click += async (s, e) => await StartAiTestAsync();
            aiTestStop.Click += (s, e) => StopAiTest();
            aiTestRefresh.Click += (s, e) => RefreshDevices();
            aiTestMeterTimer.Tick += (s, e) => UpdateAiTestLevels();
            aiTestVolume.ValueChanged += (s, e) =>
            {
                UiText.Set(aiTestVolumeLabel, "TestVolume", aiTestVolume.Value);
                if (aiTestMonitorVolume != null) aiTestMonitorVolume.Volume = aiTestVolume.Value / 100f;
            };
            UpdateAiTestControls();
            return card;
        }

        internal void SetAiTestAvailable(bool available)
        {
            aiTestAllowed = available;
            UpdateAiTestControls();
        }

        private void UpdateAiTestControls()
        {
            bool busy = aiTestCancellation != null;
            aiTestVoicePicker.SetAvailable(!busy && capture == null && aiTestAllowed);
            aiTestStart.Enabled = !busy && capture == null && aiTestAllowed && acquireAiPreview != null;
            aiTestStop.Enabled = busy && !aiTestCancellation.IsCancellationRequested;
            aiTestMicrophone.Enabled = aiTestDestination.Enabled = aiTestRefresh.Enabled = !busy && capture == null;
            aiTestUnavailable.Visible = !busy && (!aiTestAllowed || capture != null);
            UiText.Set(aiTestStart, busy && aiTestCapture == null ? "AiTestPreparing" : "AiTestStart");
        }

        private static void ValidateAiTestDevices(DeviceItem input, DeviceItem output)
        {
            if (IsCable(input.Name) || IsCable(output.Name)
                || input.Number < 0 || output.Number < 0
                || input.Number >= WaveIn.DeviceCount || output.Number >= WaveOut.DeviceCount
                || WaveIn.GetCapabilities(input.Number).ProductName != input.Name
                || WaveOut.GetCapabilities(output.Number).ProductName != output.Name)
                throw UiText.Error("DevicesChanged");
        }

        private async Task EnsureTestVoiceAsync(AiVoiceDefinition voice, CancellationTokenSource cancellation)
        {
            bool downloading = true;
            aiTestProgress.Style = ProgressBarStyle.Blocks;
            aiTestProgress.Value = 0;
            aiTestProgress.Visible = true;
            UiText.Set(aiTestStatus, "AiVoiceDownloading", voice.Name, 0, 0m, voice.Bytes / 1000000m);
            var progress = new Progress<AiInstallProgress>(value =>
            {
                if (!downloading || IsDisposed || Disposing || cancellation.IsCancellationRequested) return;
                aiTestProgress.Value = value.Percent;
                UiText.Set(aiTestStatus, value.Stage == AiInstallStage.Verify ? "AiVoiceVerifying" : "AiVoiceDownloading",
                    voice.Name, value.Percent, value.Completed / 1000000m, value.Total / 1000000m);
            });
            try { await aiTestVoicePicker.Store.EnsureAsync(voice, progress, cancellation.Token); }
            finally { downloading = false; }
        }

        private async Task DownloadAiTestVoiceAsync()
        { await ManageAiTestVoiceAsync(false); }

        private async Task ManageAiTestVoiceAsync(bool remove)
        {
            if (IsDisposed || Disposing || capture != null || aiTestCancellation != null || !aiTestAllowed || acquireAiPreview == null) return;
            if (remove && !aiTestVoicePicker.ConfirmRemoval(this)) return;
            var lease = acquireAiPreview();
            if (lease == null) return;
            var cancellation = new CancellationTokenSource();
            aiTestCancellation = cancellation;
            var voice = aiTestVoicePicker.SelectedVoice;
            SetRunning(false);
            try
            {
                lease.Runtime.Dispose();
                if (remove)
                {
                    UiText.Set(aiTestStatus, "AiVoiceRemoving", voice.Name);
                    await aiTestVoicePicker.Store.RemoveAsync(voice, cancellation.Token);
                }
                else await EnsureTestVoiceAsync(voice, cancellation);
                cancellation.Token.ThrowIfCancellationRequested();
                if (!IsDisposed && !Disposing) UiText.Set(aiTestStatus, remove ? "AiVoiceRemoved" : "AiVoiceDownloadComplete", voice.Name);
            }
            catch (OperationCanceledException) { if (!IsDisposed && !Disposing) UiText.Set(aiTestStatus, "AiCancelled"); }
            catch (Exception ex) { if (!IsDisposed && !Disposing) UiText.Set(aiTestStatus, "TestStartError", ex); }
            finally
            {
                lease.Dispose();
                aiTestCancellation = null;
                cancellation.Dispose();
                if (!IsDisposed && !Disposing)
                { aiTestProgress.Visible = false; aiTestVoicePicker.RefreshStatus(); SetRunning(capture != null); }
                else aiTestMeterTimer.Dispose();
            }
        }

        private async Task StartAiTestAsync()
        {
            if (IsDisposed || Disposing || capture != null || aiTestCancellation != null) return;
            if (!aiTestAllowed || acquireAiPreview == null)
            {
                UiText.Set(aiTestStatus, "AiTestUnavailable");
                return;
            }
            var input = aiTestMicrophone.SelectedItem as DeviceItem;
            var output = aiTestDestination.SelectedItem as DeviceItem;
            if (input == null || output == null)
            {
                UiText.Set(aiTestStatus, "AiTestSelectDevices");
                ScrollControlIntoView(aiTestStatus);
                return;
            }
            try { ValidateAiTestDevices(input, output); }
            catch (Exception ex) { UiText.Set(aiTestStatus, "TestStartError", ex); return; }
            var lease = acquireAiPreview();
            if (lease == null) { UiText.Set(aiTestStatus, "AiTestUnavailable"); return; }
            lease.Voice = aiTestVoicePicker.SelectedVoice;

            var cancellation = new CancellationTokenSource();
            aiTestCancellation = cancellation;
            aiTestError = null;
            Interlocked.Exchange(ref aiTestInputBlocks, 0);
            Interlocked.Exchange(ref aiTestOutputBlocks, 0);
            Interlocked.Exchange(ref aiTestInputTime, 0);
            Interlocked.Exchange(ref aiTestOutputTime, 0);
            SetRunning(false);
            bool installing = false;
            bool listening = false;
            try
            {
                if (!Environment.Is64BitOperatingSystem) throw UiText.Error("AiWindows64Required");
                UiText.Set(aiTestVoice, "AiTestVoice", lease.Voice.Name, lease.Voice.HasPitch ? lease.Pitch : 0);
                lease.Runtime.Dispose(); // Reload the catalog after any separately downloaded voice.
                await EnsureTestVoiceAsync(lease.Voice, cancellation);
                if (AiRuntime.FindRuntime() == null)
                {
                    installing = true;
                    aiTestProgress.Style = ProgressBarStyle.Blocks;
                    aiTestProgress.Value = 0;
                    aiTestProgress.Visible = true;
                    UiText.Set(aiTestStatus, "AiDownloading", 0, 0m, AiRuntimeInstaller.PackageBytes / 1000000m);
                    var progress = new Progress<AiInstallProgress>(value =>
                    {
                        if (!installing || IsDisposed || Disposing || cancellation.IsCancellationRequested) return;
                        aiTestProgress.Value = value.Percent;
                        string key = value.Stage == AiInstallStage.Download ? "AiDownloading"
                            : value.Stage == AiInstallStage.Verify ? "AiVerifying" : "AiExtracting";
                        UiText.Set(aiTestStatus, key, value.Percent, value.Completed / 1000000m, value.Total / 1000000m);
                    });
                    try { await new AiRuntimeInstaller().InstallAsync(progress, cancellation.Token); }
                    finally { installing = false; }
                    cancellation.Token.ThrowIfCancellationRequested();
                    aiTestProgress.Visible = false;
                }
                UiText.Set(aiTestStatus, "AiPreparing");
                aiTestProgress.Style = ProgressBarStyle.Marquee;
                aiTestProgress.MarqueeAnimationSpeed = 30;
                aiTestProgress.Visible = true;
                ScrollControlIntoView(aiTestStatus);
                await lease.Runtime.Start(aiTestVoicePicker.Store.DirectoryPath, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                UiText.Set(aiTestStatus, "AiLoading");
                await lease.Runtime.Client.PrepareVoice(lease.Voice, lease.Pitch, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                lease.Prepared = true;
                ValidateAiTestDevices(input, output); // Recheck after a potentially long download.
                StartAiTestAudio(input, output, lease.Runtime.Client.ConvertAudio, lease.Voice.BufferMilliseconds);
                listening = true;
                aiTestProgress.Visible = false;
                UpdateAiTestControls();
                UiText.Set(aiTestStatus, "AiTestRunning", input.Name, output.Name);
                ScrollControlIntoView(aiTestStatus);
                await Task.Delay(Timeout.Infinite, cancellation.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { aiTestError = ex; }
            finally
            {
                installing = false;
                await ReleaseAiTestAudioAsync();
                lease.Dispose();
                aiTestCancellation = null;
                cancellation.Dispose();
                if (IsDisposed || Disposing) aiTestMeterTimer.Dispose();
                if (!IsDisposed && !Disposing)
                {
                    aiTestProgress.Visible = false;
                    aiTestVoicePicker.RefreshStatus();
                    SetRunning(capture != null);
                    UiText.Set(aiTestStatus, aiTestError != null ? (listening ? "TestStoppedError" : "TestStartError")
                        : listening ? "AiTestStopped" : "AiCancelled", aiTestError);
                }
            }
        }

        private void StartAiTestAudio(DeviceItem input, DeviceItem output, Func<byte[], CancellationToken, Task<byte[]>> convert, int bufferMilliseconds)
        {
            var session = new WaveInEvent { DeviceNumber = input.Number, WaveFormat = new WaveFormat(44100, 1), BufferMilliseconds = bufferMilliseconds };
            var buffer = new BufferedWaveProvider(session.WaveFormat) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromMilliseconds(Math.Max(1000, bufferMilliseconds * 3)) };
            aiTestMonitorVolume = new VolumeWaveProvider16(buffer) { Volume = aiTestVolume.Value / 100f };
            aiTestPlayback = new WaveOutEvent { DeviceNumber = output.Number, DesiredLatency = 100 };
            AttachAiTestSession(session, buffer, convert);
            aiTestPlayback.PlaybackStopped += (s, e) => AiTestSessionStopped(session, e.Exception);
            aiTestPlayback.Init(aiTestMonitorVolume);
            aiTestPlayback.Play();
            session.StartRecording();
            aiTestMeterTimer.Start();
        }

        private void AttachAiTestSession(WaveInEvent session, BufferedWaveProvider buffer, Func<byte[], CancellationToken, Task<byte[]>> convert)
        {
            lock (aiTestSync)
            {
                aiTestCapture = session;
                aiTestSession = new AiVoiceSession(convert, (pcm, ms) =>
                {
                    lock (aiTestSync)
                    {
                        if (aiTestCapture == session && aiTestCancellation != null && !aiTestCancellation.IsCancellationRequested)
                        {
                            buffer.AddSamples(pcm, 0, pcm.Length);
                            Interlocked.Exchange(ref aiTestOutputPeak, PcmPeakPercent(pcm, pcm.Length));
                            Interlocked.Exchange(ref aiTestOutputTime, DateTime.UtcNow.Ticks);
                            Interlocked.Increment(ref aiTestOutputBlocks);
                        }
                    }
                }, error => AiTestSessionStopped(session, error));
            }
            session.DataAvailable += AiTestDataAvailable;
            session.RecordingStopped += (s, e) => AiTestSessionStopped(session, e.Exception);
        }

        private void AiTestDataAvailable(object sender, WaveInEventArgs e)
        {
            var session = sender as WaveInEvent;
            try
            {
                lock (aiTestSync)
                {
                    if (session == null || aiTestCapture != session || aiTestSession == null
                        || aiTestCancellation == null || aiTestCancellation.IsCancellationRequested) return;
                    Interlocked.Exchange(ref aiTestInputPeak, PcmPeakPercent(e.Buffer, e.BytesRecorded));
                    Interlocked.Exchange(ref aiTestInputTime, DateTime.UtcNow.Ticks);
                    Interlocked.Increment(ref aiTestInputBlocks);
                    aiTestSession.Enqueue(e.Buffer, e.BytesRecorded);
                }
            }
            catch (Exception ex) { AiTestSessionStopped(session, ex); }
        }

        private void AiTestSessionStopped(WaveInEvent session, Exception error)
        {
            if (!IsHandleCreated || IsDisposed || Disposing) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing || aiTestCapture != session || aiTestCancellation == null
                        || aiTestCancellation.IsCancellationRequested) return;
                    aiTestError = error;
                    StopAiTest();
                }));
            }
            catch (InvalidOperationException) { }
        }

        internal void StopAiTest()
        {
            if (aiTestCancellation == null || aiTestCancellation.IsCancellationRequested) return;
            aiTestCancellation.Cancel();
            if (!IsDisposed && !Disposing)
            {
                UiText.Set(aiTestStatus, "AiCancelling");
                UpdateAiTestControls();
            }
        }

        private async Task ReleaseAiTestAudioAsync()
        {
            aiTestMeterTimer.Stop();
            if (!IsDisposed && !Disposing) aiTestInputLevel.Value = aiTestOutputLevel.Value = 0;
            WaveInEvent oldCapture;
            AiVoiceSession oldSession;
            lock (aiTestSync)
            {
                oldCapture = aiTestCapture;
                oldSession = aiTestSession;
                aiTestCapture = null;
                aiTestSession = null;
            }
            var oldPlayback = aiTestPlayback;
            aiTestPlayback = null;
            aiTestMonitorVolume = null;
            if (oldSession != null) oldSession.Dispose();
            try { if (oldCapture != null) oldCapture.Dispose(); }
            catch (Exception ex) { if (aiTestError == null) aiTestError = ex; }
            try { if (oldPlayback != null) oldPlayback.Dispose(); }
            catch (Exception ex) { if (aiTestError == null) aiTestError = ex; }
            // Keep the engine reserved until the last conversion request has stopped.
            if (oldSession != null) await oldSession.Completion;
        }

        private static int PcmPeakPercent(byte[] pcm, int length)
        {
            int peak = 0;
            for (int i = 0; i + 1 < length; i += 2)
                peak = Math.Max(peak, Math.Abs((int)(short)(pcm[i] | (pcm[i + 1] << 8))));
            return Math.Min(100, (peak * 100 + 32767) / 32768);
        }

        private void UpdateAiTestLevels()
        {
            long recent = DateTime.UtcNow.AddSeconds(-1).Ticks;
            aiTestInputLevel.Value = Interlocked.Read(ref aiTestInputTime) > recent ? Volatile.Read(ref aiTestInputPeak) : 0;
            aiTestOutputLevel.Value = Interlocked.Read(ref aiTestOutputTime) > recent
                ? (Volatile.Read(ref aiTestOutputPeak) * aiTestVolume.Value + 99) / 100 : 0;
        }
    }
}
