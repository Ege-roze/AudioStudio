using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ses_uyg
{
    public partial class Form1
    {
        private bool libraryLoadFailed;
        private SoundPlayback pendingSound;
        private SoundPlayback playingSound; // Protected by audioSync.
        private int playingSoundRequest;
        private int soundRequestId;

        private void btnAddSound_Click(object sender, EventArgs e)
        {
            if (libraryLoadFailed) return;
            using (var dialog = new OpenFileDialog { Filter = UiText.Get("FileFilter"), Title = UiText.Get("AddSound"), Multiselect = true })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                var updated = new List<SoundItem>(soundList);
                bool duplicate = false;
                foreach (string path in dialog.FileNames)
                {
                    if (updated.Any(sound => string.Equals(sound.Path, path, StringComparison.OrdinalIgnoreCase))) duplicate = true;
                    else updated.Add(new SoundItem { Name = Path.GetFileNameWithoutExtension(path), Path = path });
                }
                if (updated.Count != soundList.Count) TryCommitSoundList(updated);
                if (duplicate) MessageBox.Show(UiText.Get("DuplicateSounds"), UiText.Get("DuplicateTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnDeleteSound_Click(object sender, EventArgs e)
        {
            var selected = listBoxSounds.SelectedItem as SoundItem;
            if (selected == null || libraryLoadFailed) return;
            var updated = new List<SoundItem>(soundList);
            updated.Remove(selected);
            if (!TryCommitSoundList(updated)) return;
            StopSoundPlayback();
            ShowSoundStatus("SoundStopped");
        }

        private async void ListBoxSounds_DoubleClick(object sender, EventArgs e)
        {
            var selected = listBoxSounds.SelectedItem as SoundItem;
            if (selected == null || installingCable || waveIn == null) return;
            if (!File.Exists(selected.Path))
            {
                ShowSoundStatus("SoundMissing", true, selected.Path);
                return;
            }
            await LoadCustomSoundAsync(selected.Path);
        }

        private void listBoxSounds_SelectedIndexChanged(object sender, EventArgs e) { }

        private void LoadSoundListFromDisk()
        {
            try
            {
                soundList = SoundLibraryStore.Load(configFilePath);
                libraryLoadFailed = false;
            }
            catch (Exception ex)
            {
                libraryLoadFailed = true;
                libraryStatus.Visible = true;
                UiText.Set(libraryStatus, "LibraryLoadError", ex);
            }
            btnAddSound.Enabled = !libraryLoadFailed;
        }

        private bool TryCommitSoundList(List<SoundItem> updated)
        {
            if (libraryLoadFailed) return false;
            try
            {
                SoundLibraryStore.Save(configFilePath, updated);
                soundList = updated;
                libraryStatus.Visible = false;
                UpdateListBox();
                return true;
            }
            catch (Exception ex)
            {
                libraryStatus.Visible = true;
                UiText.Set(libraryStatus, "LibrarySaveError", ex);
                return false;
            }
        }

        private void UpdateListBox()
        {
            var selected = listBoxSounds.SelectedItem as SoundItem;
            string query = txtSearch.Text.Trim();
            listBoxSounds.BeginUpdate();
            try
            {
                listBoxSounds.Items.Clear();
                foreach (var sound in soundList)
                    if (string.IsNullOrEmpty(query) || sound.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        listBoxSounds.Items.Add(sound);
                if (selected != null && listBoxSounds.Items.Contains(selected)) listBoxSounds.SelectedItem = selected;
            }
            finally { listBoxSounds.EndUpdate(); }
            UpdateSoundSelection();
        }

        private async Task LoadCustomSoundAsync(string path)
        {
            var session = waveIn;
            if (session == null || IsDisposed || Disposing) return;
            StopSoundPlayback();
            int request = soundRequestId;
            var sound = new SoundPlayback(path);
            pendingSound = sound;
            bool published = false;
            ShowSoundStatus("SoundLoading", false, Path.GetFileName(path));
            try
            {
                await sound.Ready;
                if (IsDisposed || Disposing || request != soundRequestId || pendingSound != sound || waveIn != session) return;
                lock (audioSync)
                {
                    playingSound = sound;
                    playingSoundRequest = request;
                }
                pendingSound = null;
                published = true;
                ShowSoundStatus("SoundPlaying", false, Path.GetFileName(path));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing && request == soundRequestId) ShowSoundStatus("SoundLoadError", true, ex);
            }
            finally
            {
                if (pendingSound == sound) pendingSound = null;
                if (!published) sound.Dispose();
            }
        }

        private void StopSoundPlayback()
        {
            soundRequestId++;
            var pending = pendingSound;
            pendingSound = null;
            SoundPlayback playing;
            lock (audioSync)
            {
                playing = playingSound;
                playingSound = null;
            }
            if (pending != null) pending.Dispose();
            if (playing != null && playing != pending) playing.Dispose();
        }

        private void SoundPlaybackFinished(SoundPlayback sound, int request)
        {
            var error = sound.Error;
            sound.Dispose();
            PostToUi(() =>
            {
                if (request != soundRequestId) return;
                if (error == null) ShowSoundStatus("SoundFinished");
                else ShowSoundStatus("SoundLoadError", true, error);
            });
        }

        private void ShowSoundStatus(string key, bool error = false, params object[] arguments)
        {
            if (IsDisposed || Disposing || soundStatus == null) return;
            soundStatus.ForeColor = error ? Color.FromArgb(153, 27, 27) : Accent;
            soundStatus.Visible = true;
            UiText.Set(soundStatus, key, arguments);
        }
    }
}
