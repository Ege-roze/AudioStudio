using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Dsp;
using System.IO;
using System.Xml.Serialization;
namespace ses_uyg
{
    public partial class Form1 : Form
    {
        // NAudio Ses Bileşenleri
        private WaveInEvent waveIn;
        private WaveOutEvent waveOut;
        private BufferedWaveProvider waveProvider;
        private SmbPitchShifter pitchShifter;
        private ComboBox microphoneSelector;
        private ComboBox cableSelector;
        private Button refreshDevices;
        private Label routingStatus;
        private TextBox discordInputDevices;
        private Button installCable;
        private Button uninstallCable;
        private bool installingCable;
        private AudioTestForm audioTestForm;
        private Button testAudioButton;

        private sealed class AudioDeviceItem
        {
            public int Number { get; set; }
            public string Name { get; set; }
            public override string ToString() { return Name; }
        }

        // 3 Bant Grafik Ekolayzır Filtreleri
        private BiQuadFilter eqBassFilter;
        private BiQuadFilter eqMidFilter;
        private BiQuadFilter eqTrebleFilter;

        // Ekolayzır Kazanç Değerleri (dB)
        private float eqBassGain = 0f;
        private float eqMidGain = 0f;
        private float eqTrebleGain = 0f;

        // Ses Tuzağı (Echo) Bileşenleri
        private Queue<float> echoBufferQueue = new Queue<float>();
        private int echoSampleDelay = 0;
        private const float EchoFeedback = 0.4f;

        // SOUNDBOARD LİSTE DEĞİŞKENLERİ
        private List<SoundItem> soundList = new List<SoundItem>();
        private string configFilePath = Path.Combine(Application.StartupPath, "soundboard_config.xml");

        // Efekt Ayarları
        private float dogshitMultiplier = 1f;
        private float pitchFactor = 1.0f;

        public Form1() : this(ApplicationSettingsStore.DefaultDirectory) { }

        internal Form1(string settingsDirectory)
        {
            applicationSettingsStore = new ApplicationSettingsStore(settingsDirectory);
            applicationSettings = applicationSettingsStore.Load();
            InitializeComponent();
            InitializeAudioRouting();
            InitializeMainLayout();

            // Filtreleri başlangıç değerleri (0 dB) ile oluştur
            UpdateFilters();
            pitchShifter = new SmbPitchShifter();

            // Tasarım dosyasında bağlı olmayan kaydırıcı olayları.
            trackBarEQBass.Scroll += trackBarEQBass_Scroll;
            trackBarEQMid.Scroll += trackBarEQMid_Scroll;
            trackBarEQTreble.Scroll += trackBarEQTreble_Scroll;
            trackBarEcho.Scroll += trackBarEcho_Scroll;

            // TrackBar Etiket Ayarlarını Başlat
            UiText.Set(lblEQBass, "Bass", 0);
            UiText.Set(lblEQMid, "Mid", 0);
            UiText.Set(lblEQTreble, "Treble", 0);
            UiText.Set(lblDogshit, "DistortionOff");
            UiText.Set(lblPitch, "PitchNormal");
            UiText.Set(lblEcho, "EchoOff");

            // Kayıtlı kütüphaneyi yükle; arama tüm liste güncellemelerinde korunur.
            LoadSoundListFromDisk();
            UpdateListBox();

            txtSearch.Text = "";

            txtSearch.TextChanged += txtSearch_TextChanged;
            SetAudioRunning(false);
            ApplicationTheme.Apply(this);
        }


        private void InitializeAudioRouting()
        {
            int top = ClientSize.Height + 8;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + 215);
            microphoneSelector = new ComboBox { Left = 12, Top = top + 22, Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(microphoneSelector);
            cableSelector = new ComboBox { Left = 380, Top = top + 22, Width = ClientSize.Width - 392, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(cableSelector);
            refreshDevices = UiText.Bind(new GlassButton { Left = 12, Top = top + 55, Width = 130 }, "Refresh");
            refreshDevices.Click += (sender, args) => RefreshAudioDevices();
            Controls.Add(refreshDevices);
            installCable = UiText.Bind(new GlassButton { Left = 150, Top = top + 55, Width = 135 }, "Install");
            installCable.Click += InstallCable_Click;
            Controls.Add(installCable);
            uninstallCable = UiText.Bind(new GlassButton(), "Uninstall");
            uninstallCable.Click += UninstallCable_Click;
            Controls.Add(uninstallCable);
            testAudioButton = UiText.Bind(new GlassButton { Left = ClientSize.Width - 110, Top = top + 55, Width = 98 }, "Test");
            testAudioButton.Click += (sender, args) =>
            {
                if (audioTestForm == null || audioTestForm.IsDisposed)
                {
                    audioTestForm = new AudioTestForm(() =>
                    {
                        if (installingCable) return false;
                        if (waveIn == null) btnStart_Click(this, EventArgs.Empty);
                        return waveIn != null;
                    }, AcquireAiPreview);
                    audioTestForm.SelectAiVoice(aiVoicePicker.SelectedVoice);
                    audioTestForm.Show(this);
                    UpdateAiControls();
                }
                else audioTestForm.Activate();
            };
            Controls.Add(testAudioButton);
            var cableWebsite = UiText.Bind(new LinkLabel { AutoSize = true, Left = 300, Top = top + 59 }, "License");
            cableWebsite.LinkClicked += (sender, args) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://vb-audio.com/Services/licensing.htm") { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(UiText.Get("PageError", ex), UiText.Get("AppTitle")); }
            };
            Controls.Add(cableWebsite);
            routingStatus = new Label { Left = 12, Top = top + 87, Width = ClientSize.Width - 24, Height = 55 };
            Controls.Add(routingStatus);
            discordInputDevices = new TextBox { Left = 12, Top = top + 165, Width = ClientSize.Width - 24, Height = 40,
                ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
            Controls.Add(discordInputDevices);
            btnStop.Enabled = false;
            RefreshAudioDevices();
        }

        private static bool IsCableInput(string name)
        {
            return name.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0
                && name.IndexOf("Input", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshAudioDevices()
        {
            string microphoneName = microphoneSelector.SelectedItem == null ? applicationSettings.MicrophoneName : microphoneSelector.SelectedItem.ToString();
            string outputName = cableSelector.SelectedItem == null ? applicationSettings.OutputName : cableSelector.SelectedItem.ToString();
            microphoneSelector.Items.Clear();
            cableSelector.Items.Clear();
            UiText.Set(discordInputDevices, "CableOutputMissing");
            try
            {
                var cableOutputs = new List<string>();
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    string name = WaveIn.GetCapabilities(i).ProductName;
                    // Output bir kayıt ucudur: diğer uygulama buradan işlenmiş sesi alır.
                    if (name.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0
                        && name.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0)
                        cableOutputs.Add(name);
                    // Sanal kabloyu tekrar girişe bağlamak bir geri besleme döngüsü oluşturur.
                    if (name.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) < 0)
                        microphoneSelector.Items.Add(new AudioDeviceItem { Number = i, Name = name });
                }
                if (cableOutputs.Count > 0)
                    UiText.SetLiteral(discordInputDevices, string.Join(Environment.NewLine, cableOutputs));
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    string name = WaveOut.GetCapabilities(i).ProductName;
                    if (IsCableInput(name))
                        cableSelector.Items.Add(new AudioDeviceItem { Number = i, Name = name });
                }
                // Mikrofonu kullanıcı seçer; ilk aygıt bir stereo mix veya başka sanal giriş olabilir.
                if (cableSelector.Items.Count == 1) cableSelector.SelectedIndex = 0;
                RestoreDeviceChoice(microphoneSelector, microphoneName);
                RestoreDeviceChoice(cableSelector, outputName);
                UiText.Set(routingStatus, cableSelector.Items.Count == 0 ? "CableMissing" : "RoutingReady");
            }
            catch (Exception ex)
            {
                UiText.Set(routingStatus, "DevicesError", ex);
                UiText.Set(discordInputDevices, "RecordingDevicesError");
            }
            installCable.Enabled = !installingCable && !aiPreviewActive && waveIn == null && cableSelector.Items.Count == 0;
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.ReloadDevices();
        }

        private async void InstallCable_Click(object sender, EventArgs e)
        {
            if (installingCable || aiPreviewActive || waveIn != null) return;
            await RunCableMaintenanceAsync(false);
        }

        private async void UninstallCable_Click(object sender, EventArgs e)
        {
            if (installingCable || aiPreviewActive) return;
            // Kaldırmadan önce bu uygulamanın açtığı kablo uçlarını serbest bırak.
            btnStop_Click(this, EventArgs.Empty);
            await RunCableMaintenanceAsync(true);
        }

        private async Task RunCableMaintenanceAsync(bool uninstall)
        {
            installingCable = true;
            UpdateAiControls();
            installCable.Enabled = false;
            uninstallCable.Enabled = false;
            btnStart.Enabled = false;
            refreshDevices.Enabled = false;
            microphoneSelector.Enabled = false;
            cableSelector.Enabled = false;
            UiText.Set(routingStatus, uninstall ? "Uninstalling" : "Installing");
            try
            {
                int exitCode = await VbCableInstaller.RunAsync();
                RefreshAudioDevices();
                UiText.Set(routingStatus, uninstall ? "Uninstalled" : "Installed", exitCode);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                UiText.Set(routingStatus, uninstall ? "UninstallCancelled" : "InstallCancelled");
            }
            catch (Exception ex)
            {
                UiText.Set(routingStatus, uninstall ? "UninstallError" : "InstallError", ex);
            }
            finally
            {
                installingCable = false;
                installCable.Enabled = cableSelector.Items.Count == 0;
                uninstallCable.Enabled = true;
                btnStart.Enabled = true;
                refreshDevices.Enabled = true;
                microphoneSelector.Enabled = true;
                cableSelector.Enabled = true;
                UpdateAiControls();
            }
        }

        private void UpdateFilters()
        {
            lock (audioSync)
            {
                // 100 Hz: bas, 1 kHz: orta, 5 kHz: tiz.
                eqBassFilter = BiQuadFilter.PeakingEQ(44100, 100f, 0.707f, eqBassGain);

                eqMidFilter = BiQuadFilter.PeakingEQ(44100, 1000f, 0.707f, eqMidGain);

                eqTrebleFilter = BiQuadFilter.PeakingEQ(44100, 5000f, 0.707f, eqTrebleGain);
            }
        }

        
        #region TRACKBAR OLAYLARI

        private void trackBarEQBass_Scroll(object sender, EventArgs e)
        {
            eqBassGain = trackBarEQBass.Value;
            UiText.Set(lblEQBass, "Bass", eqBassGain);
            UpdateFilters();
        }

        private void trackBarEQMid_Scroll(object sender, EventArgs e)
        {
            eqMidGain = trackBarEQMid.Value;
            UiText.Set(lblEQMid, "Mid", eqMidGain);
            UpdateFilters();
        }

        private void trackBarEQTreble_Scroll(object sender, EventArgs e)
        {
            eqTrebleGain = trackBarEQTreble.Value;
            UiText.Set(lblEQTreble, "Treble", eqTrebleGain);
            UpdateFilters();
        }

        private void trackBarDogshit_Scroll(object sender, EventArgs e)
        {
            SetDistortion(trackBarDogshit.Value);
            if (dogshitMultiplier == 1) UiText.Set(lblDogshit, "DistortionOff");
            else if (dogshitMultiplier < 15) UiText.Set(lblDogshit, "DistortionLow", dogshitMultiplier);
            else if (dogshitMultiplier < 35) UiText.Set(lblDogshit, "DistortionHigh", dogshitMultiplier);
            else UiText.Set(lblDogshit, "DistortionExtreme", dogshitMultiplier);
        }

        private void trackBarPitch_Scroll(object sender, EventArgs e)
        {
            SetPitch(trackBarPitch.Value / 10f);
            if (pitchFactor == 1.0f) UiText.Set(lblPitch, "PitchNormal");
            else if (pitchFactor < 1.0f) UiText.Set(lblPitch, "PitchLow", pitchFactor);
            else UiText.Set(lblPitch, "PitchHigh", pitchFactor);
        }

        private void trackBarEcho_Scroll(object sender, EventArgs e)
        {
            int echoLevel = trackBarEcho.Value;
            if (echoLevel == 0)
            {
                UiText.Set(lblEcho, "EchoOff");
                SetEchoDelay(0);
            }
            else
            {
                int delayMs = echoLevel * 120;
                UiText.Set(lblEcho, "EchoOn", delayMs);
                SetEchoDelay((int)((44100f / 1000f) * delayMs));
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            // Bütün TrackBar ve Efekt Değerlerini Sıfırla
            trackBarEQBass.Value = 0;
            trackBarEQMid.Value = 0;
            trackBarEQTreble.Value = 0;
            trackBarDogshit.Value = 1;
            trackBarPitch.Value = 10;
            trackBarEcho.Value = 0;

            eqBassGain = 0f;
            eqMidGain = 0f;
            eqTrebleGain = 0f;
            SetDistortion(1f);
            SetPitch(1.0f);
            SetEchoDelay(0);

            UpdateFilters();

            // Etiketleri Eski Haline Getir
            UiText.Set(lblEQBass, "Bass", 0);
            UiText.Set(lblEQMid, "Mid", 0);
            UiText.Set(lblEQTreble, "Treble", 0);
            UiText.Set(lblDogshit, "DistortionOff");
            UiText.Set(lblPitch, "PitchNormal");
            UiText.Set(lblEcho, "EchoOff");

            // Listeyi mevcut arama filtresi ve seçimle yenile.
            UpdateListBox();
        }

        #endregion


        
        private void btnRadioMode_Click(object sender, EventArgs e)
        {
            // Telsiz Efekti: Basları tamamen kısar, tizleri (cızırtıyı) sonuna kadar açar. 
            // Ses tonunu (pitch) da hafif telsiz mekanikliğine çeker.

            eqBassGain = -15f;    // Bas yok
            eqMidGain = 5f;       // Orta sesler net
            eqTrebleGain = 15f;   // Tizler maksimum (telsiz cızırtısı)
            SetPitch(1.3f);   // Sesi telsiz gibi biraz inceltiyoruz

            // Değişen gain ve pitch değerlerini anlık ses motoruna aktarmak için filtreleri yeniliyoruz
            UpdateFilters();

            // Arayüzdeki TrackBar çubuklarını ve etiketleri de bu yeni değerlere eşitleyelim:
            trackBarEQBass.Value = -15;
            trackBarEQMid.Value = 5;
            trackBarEQTreble.Value = 15;
            trackBarPitch.Value = 13; // 1.3f değerine denk gelir

            UiText.Set(lblEQBass, "Bass", -15);
            UiText.Set(lblEQMid, "Mid", 5);
            UiText.Set(lblEQTreble, "Treble", 15);
            UiText.Set(lblPitch, "PitchRadio", pitchFactor);

            MessageBox.Show(UiText.Get("RadioActive"), UiText.Get("EffectChanged"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnCaveMode_Click(object sender, EventArgs e)
        {
            // Mağara Modu: Devasa bir eko (echo) verir, ses tonunu kalınlaştırır 
            // ve basları artırarak derinlik hissi yaratır.

            eqBassGain = 12f;     // Derin baslar
            eqMidGain = -2f;
            eqTrebleGain = -8f;   // Tizleri boğuyoruz ki mağara derinliği oluşsun
            SetPitch(0.8f);   // Ses tonunu kalınlaştırıyoruz (Canavar efekti)

            // Filtreleri yeni EQ değerleri ile yeniden oluştur
            UpdateFilters();

            // Yankıyı (Echo) maksimuma çıkarıyoruz ve arka plandaki delay süresini hesaplıyoruz
            trackBarEcho.Value = trackBarEcho.Maximum;
            int delayMs = trackBarEcho.Value * 120;
            UiText.Set(lblEcho, "EchoOn", delayMs);
            SetEchoDelay((int)((44100f / 1000f) * delayMs));

            // TrackBar'ları görsel olarak güncelle
            trackBarEQBass.Value = 12;
            trackBarEQMid.Value = -2;
            trackBarEQTreble.Value = -8;
            trackBarPitch.Value = 8; // 0.8f değerine denk gelir

            UiText.Set(lblEQBass, "Bass", 12);
            UiText.Set(lblEQMid, "Mid", -2);
            UiText.Set(lblEQTreble, "Treble", -8);
            UiText.Set(lblPitch, "PitchCave", pitchFactor);

            MessageBox.Show(UiText.Get("CaveActive"), UiText.Get("EffectChanged"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            UpdateListBox();
        }
        private void btnAnimeMode_Click(object sender, EventArgs e)
        {
            // 1. Sivrisinek vızıltısı için ses tonunu (Pitch) yine o cırtlak seviyeye (1.7x) çekiyoruz
            SetPitch(1.7f);
            trackBarPitch.Value = 17;
            UiText.Set(lblPitch, "PitchMosquito", pitchFactor);

            // 2. EQ Ayarları: Basları tamamen yok et, kulak tırmalayan mid ve tizleri kökle!
            eqBassGain = -15f;
            eqMidGain = 12f;
            eqTrebleGain = 15f;

            trackBarEQBass.Value = -15;
            trackBarEQMid.Value = 12;
            trackBarEQTreble.Value = 15;

            UiText.Set(lblEQBass, "Bass", -15);
            UiText.Set(lblEQMid, "Mid", 12);
            UiText.Set(lblEQTreble, "Treble", 15);

            // 3. Diğer efektleri kapat
            trackBarEcho.Value = 0;
            SetEchoDelay(0);
            UiText.Set(lblEcho, "EchoOff");

            trackBarDogshit.Value = 1;
            SetDistortion(1f);
            UiText.Set(lblDogshit, "DistortionOff");

            // Filtreleri yeni kulak tırmalayan değerlerle güncelle
            UpdateFilters();

            // Eğlenceli bir bildirim mesajı
            MessageBox.Show(UiText.Get("MosquitoActive"), UiText.Get("EffectChanged"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public class SoundItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public override string ToString() { return Name; }
    }
}
