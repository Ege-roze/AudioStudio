using System;
using System.Windows.Forms;
using NAudio.Dsp;
using NAudio.Wave;

namespace ses_uyg
{
    public partial class Form1
    {
        // No device disposal, file I/O, or UI dispatch may wait while this lock is held.
        private readonly object audioSync = new object();

        private void SetPitch(float value)
        {
            lock (audioSync)
            {
                if (pitchFactor != value) pitchShifter = new SmbPitchShifter();
                pitchFactor = value;
            }
        }

        private void SetDistortion(float value)
        {
            lock (audioSync) dogshitMultiplier = value;
        }

        private void SetEchoDelay(int samples)
        {
            lock (audioSync)
            {
                if (echoSampleDelay != samples) echoBufferQueue.Clear();
                echoSampleDelay = samples;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (waveIn != null || installingCable || aiPreparation != null || aiPreviewActive) return;
            var microphone = microphoneSelector.SelectedItem as AudioDeviceItem;
            var cable = cableSelector.SelectedItem as AudioDeviceItem;
            if (microphone == null || cable == null)
            {
                MessageBox.Show(UiText.Get("SelectRouting"), UiText.Get("Routing"));
                return;
            }
            try
            {
                if (microphone.Number >= WaveIn.DeviceCount || cable.Number >= WaveOut.DeviceCount
                    || WaveIn.GetCapabilities(microphone.Number).ProductName != microphone.Name
                    || WaveOut.GetCapabilities(cable.Number).ProductName != cable.Name
                    || !IsCableInput(cable.Name))
                    throw UiText.Error("DevicesChanged");

                var session = new WaveInEvent { DeviceNumber = microphone.Number, WaveFormat = new WaveFormat(44100, 1), BufferMilliseconds = aiEnabled ? (activeAiVoice ?? aiVoicePicker.SelectedVoice).BufferMilliseconds : 100 };
                lock (audioSync)
                {
                    waveIn = session;
                    echoBufferQueue.Clear();
                    pitchShifter = new SmbPitchShifter();
                    UpdateFilters();
                    waveProvider = new BufferedWaveProvider(session.WaveFormat) { DiscardOnBufferOverflow = true };
                    waveOut = new WaveOutEvent { DeviceNumber = cable.Number };
                }
                AttachAudioSessionHandlers(session, waveOut);
                waveOut.Init(waveProvider);
                waveOut.Play();
                StartAiSession(session);
                session.StartRecording();
                SetAudioRunning(true);
                UiText.Set(routingStatus, "RoutingReady");
            }
            catch (Exception ex)
            {
                StopAudio();
                SetAudioRunning(false);
                UiText.Set(routingStatus, "StartError", ex);
            }
        }

        private void AttachAudioSessionHandlers(WaveInEvent session, WaveOutEvent output)
        {
            session.DataAvailable += WaveIn_DataAvailable;
            session.RecordingStopped += (s, args) => AudioSessionStopped(session, args.Exception);
            output.PlaybackStopped += (s, args) => AudioSessionStopped(session, args.Exception);
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            var session = sender as WaveInEvent;
            try
            {
                lock (audioSync)
                {
                    if (session == null || waveIn != session || waveProvider == null) return;
                    if (aiSession != null)
                    {
                        aiSession.Enqueue(e.Buffer, e.BytesRecorded);
                        return;
                    }
                }
                ProcessMicrophoneAudio(session, e.Buffer, e.BytesRecorded);
            }
            catch (Exception ex) { AudioSessionStopped(session, ex); }
        }

        private void ProcessMicrophoneAudio(WaveInEvent session, byte[] buffer, int bytesRecorded)
        {
            SoundPlayback finishedSound = null;
            int finishedRequest = 0;
            try
            {
                lock (audioSync)
                {
                    // Ignore late callbacks belonging to an already stopped session.
                    if (session == null || waveIn != session || waveProvider == null) return;
                    int sampleCount = bytesRecorded / 2;
                    if (sampleCount == 0) return;
                    var floats = new float[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                        floats[i] = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]) / 32768f;

                    if (pitchFactor != 1f) pitchShifter.PitchShift(pitchFactor, sampleCount, 44100, floats);
                    if (playingSound != null)
                    {
                        playingSound.MixInto(floats, sampleCount);
                        if (playingSound.Finished)
                        {
                            finishedSound = playingSound;
                            finishedRequest = playingSoundRequest;
                            playingSound = null;
                        }
                    }
                    if (echoSampleDelay > 0)
                    {
                        for (int i = 0; i < sampleCount; i++)
                        {
                            float current = floats[i];
                            float delayed = echoBufferQueue.Count >= echoSampleDelay ? echoBufferQueue.Dequeue() : 0f;
                            floats[i] = current + delayed * EchoFeedback;
                            echoBufferQueue.Enqueue(Math.Max(-1f, Math.Min(1f, current + delayed * 0.3f)));
                        }
                    }
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float current = eqTrebleFilter.Transform(eqMidFilter.Transform(eqBassFilter.Transform(floats[i] * 32768f)));
                        if (dogshitMultiplier > 1)
                        {
                            current *= dogshitMultiplier;
                            int factor = (int)(dogshitMultiplier / 2) + 1;
                            current = ((int)current / factor) * factor;
                        }
                        short sample = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, current));
                        buffer[i * 2] = (byte)(sample & 0xFF);
                        buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                    }
                    waveProvider.AddSamples(buffer, 0, bytesRecorded);
                }
                if (finishedSound != null) SoundPlaybackFinished(finishedSound, finishedRequest);
            }
            catch (Exception ex)
            {
                if (finishedSound != null) finishedSound.Dispose();
                AudioSessionStopped(session, ex);
            }
        }

        private void PostToUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try { BeginInvoke(new Action(() => { if (!IsDisposed && !Disposing) action(); })); }
            catch (InvalidOperationException) { } // The window may have closed before dispatch.
        }

        private void AudioSessionStopped(WaveInEvent session, Exception error)
        {
            PostToUi(() => HandleAudioSessionStopped(session, error));
        }

        private void HandleAudioSessionStopped(WaveInEvent session, Exception error)
        {
            if (session == null || waveIn != session) return;
            var cleanupError = StopAudio();
            SetAudioRunning(false);
            UiText.Set(routingStatus, error == null && cleanupError == null ? "StreamEnded" : "StreamError", error ?? cleanupError);
            if (aiEnabled && error != null) UiText.Set(aiStatus, "AiStreamError", error);
            ShowSoundStatus("SoundStopped");
        }

        private Exception StopAudio()
        {
            // A stopped stream must not be reactivated by a late AI preparation.
            if (aiPreparation != null) aiPreparation.Cancel();
            WaveInEvent oldInput;
            WaveOutEvent oldOutput;
            AiVoiceSession oldAi;
            lock (audioSync)
            {
                oldInput = waveIn;
                oldOutput = waveOut;
                oldAi = aiSession;
                aiSession = null;
                waveIn = null;
                waveOut = null;
                waveProvider = null;
                echoBufferQueue.Clear();
            }
            if (oldAi != null) oldAi.Dispose();
            StopSoundPlayback();
            Exception error = null;
            try { if (audioTestForm != null && !audioTestForm.IsDisposed) audioTestForm.StopTest(); }
            catch (Exception ex) { error = ex; }
            try { if (oldInput != null) oldInput.Dispose(); }
            catch (Exception ex) { if (error == null) error = ex; }
            try { if (oldOutput != null) oldOutput.Dispose(); }
            catch (Exception ex) { if (error == null) error = ex; }
            return error;
        }

        private void SetAudioRunning(bool running)
        {
            // Gate the containers so selection/error restrictions on their children survive.
            aiVoiceSection.Enabled = effectsSection.Enabled = soundLibrarySection.Enabled = running;
            audioLockedHint.Visible = !running;
            btnStart.Enabled = !running && !installingCable;
            btnStop.Enabled = running;
            microphoneSelector.Enabled = cableSelector.Enabled = refreshDevices.Enabled = !running && !installingCable;
            installCable.Enabled = !running && !installingCable && cableSelector.Items.Count == 0;
            UpdateAiControls();
            if (!running && aiEnabled && aiStatus != null) UiText.Set(aiStatus, "AiReady");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            var error = StopAudio();
            SetAudioRunning(false);
            UiText.Set(routingStatus, error == null ? "StreamStopped" : "StreamCleanupError", error);
            ShowSoundStatus("SoundStopped");
        }

    }
}
