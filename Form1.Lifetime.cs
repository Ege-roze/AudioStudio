using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ses_uyg
{
    public partial class Form1
    {
        private bool exitRequested;
        private bool hiddenToTray;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayShow;
        private ToolStripMenuItem trayExit;
        private ToolStripMenuItem traySettings;
        private readonly List<Form> hiddenOwnedForms = new List<Form>();

        private void EnsureTrayIcon()
        {
            if (trayIcon != null) return;
            trayShow = new ToolStripMenuItem();
            trayShow.Click += (s, e) => RestoreFromTray();
            trayExit = new ToolStripMenuItem();
            trayExit.Click += (s, e) => ExitApplication();
            traySettings = new ToolStripMenuItem();
            traySettings.Click += (s, e) => OpenSettings();
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.AddRange(new ToolStripItem[] { trayShow, traySettings, new ToolStripSeparator(), trayExit });
            trayIcon = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, ContextMenuStrip = trayMenu };
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();
            UpdateTrayText();
        }

        private void UpdateTrayText()
        {
            if (trayIcon == null) return;
            trayIcon.Text = UiText.Get("AppTitle");
            trayShow.Text = UiText.Get("ShowApplication");
            trayExit.Text = UiText.Get("ExitApplication");
            traySettings.Text = UiText.Get("SettingsTitle");
        }

        private void HideToTray()
        {
            if (hiddenToTray) return;
            EnsureTrayIcon();
            trayIcon.Visible = true;
            hiddenToTray = true;
            hiddenOwnedForms.Clear();
            foreach (var owned in OwnedForms)
            {
                if (!owned.Visible) continue;
                hiddenOwnedForms.Add(owned);
                owned.Hide();
            }
            // Hiding preserves capture, AI preparation and any open editor's draft.
            Hide();
        }

        private void RestoreFromTray()
        {
            if (IsDisposed || Disposing) return;
            hiddenToTray = false;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            foreach (var owned in hiddenOwnedForms)
                if (!owned.IsDisposed) owned.Show();
            hiddenOwnedForms.Clear();
            Activate();
            if (trayIcon != null) trayIcon.Visible = false;
        }

        private void ExitApplication()
        {
            if (IsDisposed || Disposing) return;
            // Keep the existing driver-installation guard; do not abandon its cleanup.
            if (installingCable)
            {
                if (hiddenToTray) RestoreFromTray();
                MessageBox.Show(this, UiText.Get("CloseInstaller"), UiText.Get("MaintenanceTitle"));
                return;
            }
            exitRequested = true;
            Close();
            if (!IsDisposed) exitRequested = false; // A FormClosing subscriber may cancel.
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            if (e.CloseReason == CloseReason.UserClosing && !exitRequested && applicationSettings.CloseToTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            if (installingCable && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MessageBox.Show(this, UiText.Get("CloseInstaller"), UiText.Get("MaintenanceTitle"));
                return;
            }
            // Explicit exit and Windows shutdown both use the normal cleanup path.
            StopAudio();
            CloseAi();
            DisposeTray();
        }

        private void DisposeTray()
        {
            hiddenOwnedForms.Clear();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            if (trayMenu != null) { trayMenu.Dispose(); trayMenu = null; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (applicationSettings.StartInTray)
                BeginInvoke(new Action(() => { if (!IsDisposed && !Disposing && !exitRequested) HideToTray(); }));
        }
    }
}
