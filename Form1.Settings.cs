using System;
using System.Linq;
using System.Windows.Forms;

namespace ses_uyg
{
    public partial class Form1
    {
        private ApplicationSettingsStore applicationSettingsStore;
        private ApplicationOptions applicationSettings;
        private readonly IStartupRegistration windowsStartup = new WindowsStartupRegistration();
        private SettingsForm settingsForm;
        private Button settingsButton;
        private ToolTip settingsTip;

        internal ApplicationOptions ApplicationSettings { get { return applicationSettings.Copy(); } }
        internal bool CanChangeAudioDevices { get { return waveIn == null && aiPreparation == null && !aiPreviewActive && !installingCable; } }
        internal bool StartsWithWindows { get { return !string.IsNullOrEmpty(windowsStartup.ReadCommand()); } }

        private void OpenSettings()
        {
            if (hiddenToTray) RestoreFromTray();
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(this);
                settingsForm.Show(this);
            }
            else { settingsForm.Show(); settingsForm.Activate(); }
        }

        internal void CopyDeviceChoices(ComboBox microphones, ComboBox outputs)
        {
            CopyDeviceList(microphoneSelector, microphones);
            CopyDeviceList(cableSelector, outputs);
        }

        private static void CopyDeviceList(ComboBox source, ComboBox destination)
        {
            destination.Items.Clear();
            destination.Items.AddRange(source.Items.Cast<object>().ToArray());
            destination.SelectedItem = source.SelectedItem;
        }

        internal void RefreshSettingsDevices()
        {
            if (CanChangeAudioDevices) RefreshAudioDevices();
        }

        internal void SaveApplicationSettings(bool closeToTray, bool startInTray, bool? startWithWindows,
            object microphone, object output)
        {
            var next = applicationSettings.Copy();
            next.CloseToTray = closeToTray;
            next.StartInTray = startInTray;
            bool changeDevices = CanChangeAudioDevices;
            if (!changeDevices)
            {
                microphone = microphoneSelector.SelectedItem;
                output = cableSelector.SelectedItem;
            }
            if ((microphone != null && !microphoneSelector.Items.Contains(microphone))
                || (output != null && !cableSelector.Items.Contains(output))) throw UiText.Error("DevicesChanged");
            // A temporarily unplugged device keeps its saved preference.
            if (microphone != null) next.MicrophoneName = microphone.ToString();
            if (output != null) next.OutputName = output.ToString();
            applicationSettings = applicationSettingsStore.Save(next, windowsStartup, startWithWindows, Application.ExecutablePath);
            if (changeDevices)
            {
                microphoneSelector.SelectedItem = microphone;
                cableSelector.SelectedItem = output;
            }
        }

        private static void RestoreDeviceChoice(ComboBox selector, string preferredName)
        {
            var matches = selector.Items.Cast<object>().Where(item => string.Equals(item.ToString(), preferredName, StringComparison.Ordinal)).ToArray();
            // Device indices can change after a reboot. Ambiguous names require a fresh choice.
            if (matches.Length == 1) selector.SelectedItem = matches[0];
        }

        private void RefreshSettingsLanguage()
        {
            if (settingsButton != null)
            {
                settingsButton.AccessibleName = UiText.Get("SettingsTitle");
                settingsTip.SetToolTip(settingsButton, UiText.Get("SettingsTitle"));
            }
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshLanguage();
        }
    }
}
