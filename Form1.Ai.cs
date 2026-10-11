using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ses_uyg
{
    public partial class Form1
    {
        private readonly AiRuntime aiRuntime = new AiRuntime();
        private CancellationTokenSource aiPreparation;
        private bool aiEnabled;
        private bool aiPreviewActive;
        private AiVoiceSession aiSession; // Protected by audioSync.
        private Button aiEnableButton;
        private Button aiDisableButton;
        private NumericUpDown aiPitch;
        private Label aiStatus;
        private Label aiTiming;
        private ProgressBar aiInstallProgress;
        private DateTime aiLastTiming;
        private AiVoicePicker aiVoicePicker;
        private AiVoiceDefinition activeAiVoice;

        private Control CreateAiCard()
        {
            var card = Card("AiTitle");
            card.Name = "aiVoiceSection";
            card.Margin = new Padding(0, 0, 0, 18);
            card.Controls.Add(Caption("AiDescription"));
            aiVoicePicker = new AiVoicePicker(AiVoiceStore.Default());
            aiVoicePicker.SelectedVoice = AiVoiceCatalog.Find(applicationSettings.AiVoiceId);
            card.Controls.Add(aiVoicePicker);
            aiPitch = new NumericUpDown
            {
                Name = "aiPitch", Minimum = -12, Maximum = 24, Value = 12, Width = 65,
                BackColor = Surface, ForeColor = Ink, Margin = new Padding(8, 2, 16, 2)
            };
            aiEnableButton = UiText.Bind(new GlassButton { Name = "aiEnableButton" }, "AiEnable");
            aiDisableButton = UiText.Bind(new GlassButton { Name = "aiDisableButton", Enabled = false }, "AiDisable");
            StyleButton(aiEnableButton, Accent, Color.White);
            StyleButton(aiDisableButton);
            aiEnableButton.Click += EnableAi_Click;
            aiDisableButton.Click += (s, e) => DisableAi();
            card.Controls.Add(ButtonRow(Caption("AiPitch"), aiPitch, aiEnableButton, aiDisableButton));
            aiStatus = Caption(AiRuntime.FindRuntime() == null ? "AiNotInstalled" : "AiIdle");
            aiStatus.Name = "aiStatus";
            card.Controls.Add(aiStatus);
            aiInstallProgress = new ProgressBar
            {
                Name = "aiInstallProgress", Dock = DockStyle.Top, Height = 18,
                Margin = new Padding(0, 6, 0, 8), Visible = false
            };
            card.Controls.Add(aiInstallProgress);
            card.Controls.Add(Caption("AiInstallHint"));
            card.Controls.Add(Caption("AiNetworkDisclosure"));
            aiTiming = Caption("AiTimingHint");
            card.Controls.Add(aiTiming);
            aiVoicePicker.SelectionChanged += (s, e) =>
            {
                try
                {
                    var next = applicationSettings.Copy();
                    next.AiVoiceId = aiVoicePicker.SelectedVoice.Id;
                    applicationSettings = applicationSettingsStore.Save(next);
                }
                catch (Exception ex) { UiText.Set(aiStatus, "AiError", ex); }
                UpdateAiControls();
            };
            aiVoicePicker.DownloadRequested += async (s, e) => await DownloadSelectedVoiceAsync();
            aiVoicePicker.RemoveRequested += async (s, e) => await ManageSelectedVoiceAsync(true);
            card.Controls.Add(LegalDocumentForm.CreateLink("ModelTerms", 3));
            return card;
        }

        private async System.Threading.Tasks.Task DownloadSelectedVoiceAsync()
        { await ManageSelectedVoiceAsync(false); }

        private async System.Threading.Tasks.Task ManageSelectedVoiceAsync(bool remove)
        {
            if (aiPreparation != null || aiEnabled || aiPreviewActive || installingCable || waveIn == null) return;
            if (remove && !aiVoicePicker.ConfirmRemoval(this)) return;
            var preparation = new CancellationTokenSource();
            aiPreparation = preparation;
            var voice = aiVoicePicker.SelectedVoice;
            UpdateAiControls();
            try
            {
                if (remove)
                {
                    UiText.Set(aiStatus, "AiVoiceRemoving", voice.Name);
                    await aiVoicePicker.Store.RemoveAsync(voice, preparation.Token);
                }
                else await EnsureSelectedVoiceAsync(voice, preparation);
                preparation.Token.ThrowIfCancellationRequested();
                if (!IsDisposed && !Disposing) UiText.Set(aiStatus, remove ? "AiVoiceRemoved" : "AiVoiceDownloadComplete", voice.Name);
            }
            catch (OperationCanceledException) { if (!IsDisposed && !Disposing) UiText.Set(aiStatus, "AiCancelled"); }
            catch (Exception ex) { if (!IsDisposed && !Disposing) UiText.Set(aiStatus, "AiError", ex); }
            finally
            {
                if (aiPreparation == preparation) aiPreparation = null;
                preparation.Dispose();
                if (!IsDisposed && !Disposing) { aiInstallProgress.Visible = false; aiVoicePicker.RefreshStatus(); UpdateAiControls(); }
            }
        }

        private async System.Threading.Tasks.Task EnsureSelectedVoiceAsync(AiVoiceDefinition voice, CancellationTokenSource preparation)
        {
            bool installing = true;
            aiInstallProgress.Style = ProgressBarStyle.Blocks;
            aiInstallProgress.Value = 0;
            aiInstallProgress.Visible = true;
            UiText.Set(aiStatus, "AiVoiceDownloading", voice.Name, 0, 0m, voice.Bytes / 1000000m);
            var progress = new Progress<AiInstallProgress>(value =>
            {
                if (!installing || IsDisposed || Disposing || aiPreparation != preparation || preparation.IsCancellationRequested) return;
                aiInstallProgress.Value = value.Percent;
                UiText.Set(aiStatus, value.Stage == AiInstallStage.Verify ? "AiVoiceVerifying" : "AiVoiceDownloading",
                    voice.Name, value.Percent, value.Completed / 1000000m, value.Total / 1000000m);
            });
            try { await aiVoicePicker.Store.EnsureAsync(voice, progress, preparation.Token); }
            finally { installing = false; if (!IsDisposed && !Disposing) aiInstallProgress.Visible = false; }
        }

        private async void EnableAi_Click(object sender, EventArgs e)
        {
            if (aiPreparation != null || aiEnabled || installingCable || waveIn == null) return;
            var capture = waveIn;
            var voice = aiVoicePicker.SelectedVoice;
            var preparation = new CancellationTokenSource();
            aiPreparation = preparation;
            UpdateAiControls();
            UiText.Set(aiStatus, "AiPreparing");
            try
            {
                if (!Environment.Is64BitOperatingSystem) throw UiText.Error("AiWindows64Required");
                await EnsureSelectedVoiceAsync(voice, preparation);
                if (AiRuntime.FindRuntime() == null)
                {
                    bool installing = true;
                    aiInstallProgress.Value = 0;
                    aiInstallProgress.Visible = true;
                    UiText.Set(aiStatus, "AiDownloading", 0, 0m, AiRuntimeInstaller.PackageBytes / 1000000m);
                    var progress = new Progress<AiInstallProgress>(value =>
                    {
                        if (!installing || IsDisposed || Disposing || aiPreparation != preparation || preparation.IsCancellationRequested) return;
                        ShowAiInstallProgress(value);
                    });
                    try { await new AiRuntimeInstaller().InstallAsync(progress, preparation.Token); }
                    finally { installing = false; } // Ignore progress posted after the installer has finished.
                    preparation.Token.ThrowIfCancellationRequested();
                    aiInstallProgress.Visible = false;
                }
                UiText.Set(aiStatus, "AiPreparing");
                await aiRuntime.Start(aiVoicePicker.Store.DirectoryPath, preparation.Token);
                preparation.Token.ThrowIfCancellationRequested();
                UiText.Set(aiStatus, "AiLoading");
                await aiRuntime.Client.PrepareVoice(voice, (int)aiPitch.Value, preparation.Token);
                preparation.Token.ThrowIfCancellationRequested();
                if (IsDisposed || Disposing || waveIn != capture) return;
                aiEnabled = true;
                activeAiVoice = voice;
                UiText.Set(aiStatus, "AiReady");
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !Disposing) UiText.Set(aiStatus, "AiCancelled");
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing) UiText.Set(aiStatus, "AiError", ex);
            }
            finally
            {
                if (!aiEnabled) aiRuntime.Dispose();
                if (aiPreparation == preparation) aiPreparation = null;
                preparation.Dispose();
                if (!IsDisposed && !Disposing)
                {
                    aiInstallProgress.Visible = false;
                    aiVoicePicker.RefreshStatus();
                    UpdateAiControls();
                }
            }
            if (aiEnabled) RestartAudioForAi(capture);
        }

        private void RestartAudioForAi(NAudio.Wave.WaveInEvent capture)
        {
            if (IsDisposed || Disposing || capture == null || waveIn != capture) return;
            // Recreate capture buffers for the selected AI model or classic mode.
            btnStop_Click(this, EventArgs.Empty);
            btnStart_Click(this, EventArgs.Empty);
        }

        private void ShowAiInstallProgress(AiInstallProgress value)
        {
            aiInstallProgress.Value = value.Percent;
            string key = value.Stage == AiInstallStage.Download ? "AiDownloading"
                : value.Stage == AiInstallStage.Verify ? "AiVerifying" : "AiExtracting";
            UiText.Set(aiStatus, key, value.Percent, value.Completed / 1000000m, value.Total / 1000000m);
        }

        private void DisableAi()
        {
            if (aiPreparation != null)
            {
                aiPreparation.Cancel();
                UiText.Set(aiStatus, "AiCancelling");
                UpdateAiControls();
                return; // The preparation task owns cleanup until cancellation completes.
            }
            var capture = waveIn;
            aiEnabled = false;
            // Stop conversion before releasing the server used by its worker.
            if (capture != null) btnStop_Click(this, EventArgs.Empty);
            // Release GPU memory and stop the owned server when AI is disabled.
            aiRuntime.Dispose();
            UiText.Set(aiStatus, "AiIdle");
            UiText.Set(aiTiming, "AiTimingHint");
            UpdateAiControls();
            if (capture != null) btnStart_Click(this, EventArgs.Empty);
        }

        private void UpdateAiControls()
        {
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.UpdateDeviceAvailability();
            if (aiEnableButton == null) return;
            aiEnableButton.Enabled = aiPreparation == null && !aiEnabled && !installingCable;
            aiDisableButton.Enabled = aiEnabled || (aiPreparation != null && !aiPreparation.IsCancellationRequested);
            aiPitch.Enabled = !aiEnabled && aiPreparation == null && !aiPreviewActive && aiVoicePicker.SelectedVoice.HasPitch;
            aiVoicePicker.SetAvailable(!aiEnabled && aiPreparation == null && !aiPreviewActive && !installingCable);
            if (aiPreparation != null || aiPreviewActive)
            {
                installCable.Enabled = false;
                uninstallCable.Enabled = false;
            }
            else if (!installingCable)
            {
                installCable.Enabled = waveIn == null && cableSelector.Items.Count == 0;
                uninstallCable.Enabled = true;
            }
            if (aiPreparation != null || aiPreviewActive) btnStart.Enabled = false;
            else btnStart.Enabled = waveIn == null && !installingCable;
            if (audioTestForm != null && !audioTestForm.IsDisposed)
                audioTestForm.SetAiTestAvailable(waveIn == null && aiPreparation == null && !installingCable && !aiPreviewActive);
        }

        private AiPreviewLease AcquireAiPreview()
        {
            if (IsDisposed || Disposing || waveIn != null || aiPreparation != null || installingCable || aiPreviewActive) return null;
            aiPreviewActive = true;
            UiText.Set(audioLockedHint, "AiTestMainLocked");
            UpdateAiControls();
            // Borrow the same engine and pitch, without changing the main AI selection.
            AiPreviewLease lease = null;
            lease = new AiPreviewLease(aiRuntime, (int)aiPitch.Value, prepared =>
            {
                if (!prepared || lease.Voice.Id != aiVoicePicker.SelectedVoice.Id) aiEnabled = false;
                if (prepared) activeAiVoice = lease.Voice;
                if (!aiEnabled || IsDisposed || Disposing) aiRuntime.Dispose();
                aiPreviewActive = false;
                if (!IsDisposed && !Disposing)
                {
                    UiText.Set(audioLockedHint, "AudioLockedHint");
                    if (!aiEnabled) UiText.Set(aiStatus, AiRuntime.FindRuntime() == null ? "AiNotInstalled" : "AiIdle");
                    UpdateAiControls();
                    aiVoicePicker.RefreshStatus();
                }
            });
            lease.Voice = aiVoicePicker.SelectedVoice;
            return lease;
        }

        private void StartAiSession(NAudio.Wave.WaveInEvent capture)
        {
            if (!aiEnabled) return;
            var client = aiRuntime.Client;
            var session = new AiVoiceSession(client.ConvertAudio,
                (pcm, milliseconds) =>
                {
                    ProcessMicrophoneAudio(capture, pcm, pcm.Length);
                    if ((DateTime.UtcNow - aiLastTiming).TotalSeconds < 1) return;
                    aiLastTiming = DateTime.UtcNow;
                    PostToUi(() =>
                    {
                        if (waveIn == capture) UiText.Set(aiTiming, "AiTiming", (int)Math.Round(milliseconds), (activeAiVoice ?? aiVoicePicker.SelectedVoice).BufferMilliseconds);
                    });
                },
                error => AudioSessionStopped(capture, error));
            lock (audioSync) aiSession = session;
            UiText.Set(aiStatus, "AiStreaming");
        }

        private void CloseAi()
        {
            if (aiPreparation != null) aiPreparation.Cancel();
            if (audioTestForm != null && !audioTestForm.IsDisposed) audioTestForm.StopAiTest();
            aiRuntime.Dispose();
        }
    }
}
