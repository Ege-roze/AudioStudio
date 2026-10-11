using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ses_uyg
{
    internal sealed class AiVoiceStorage
    {
        internal int InstalledCount;
        internal long ModelBytes, PartialBytes;
        internal long TotalBytes { get { return ModelBytes + PartialBytes; } }
    }

    internal sealed class AiVoiceStore
    {
        internal static event Action<string> Changed;
        internal string DirectoryPath { get; private set; }
        internal AiVoiceStore(string directory) { DirectoryPath = Path.GetFullPath(directory); }
        internal static AiVoiceStore Default()
        {
            string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SesStudyosu", "AiVoices");
            return FindStore(user, AiRuntime.FindRuntime());
        }
        internal static AiVoiceStore FindStore(string user, string runtime)
        {
            if (Directory.Exists(user)) return new AiVoiceStore(user);
            if (runtime != null)
            {
                string legacy = Path.Combine(Path.GetDirectoryName(runtime), "ses_models");
                // Removing the first voice must not hide all the other legacy voices.
                if (Directory.Exists(legacy)) return new AiVoiceStore(legacy);
            }
            return new AiVoiceStore(user);
        }
        private string SlotPath(AiVoiceDefinition voice) { return Path.Combine(DirectoryPath, voice.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        internal string ModelPath(AiVoiceDefinition voice) { return Path.Combine(SlotPath(voice), voice.FileName); }
        private string PartialPath(AiVoiceDefinition voice) { return Path.Combine(DirectoryPath, ".voice-" + voice.Slot + ".part"); }

        internal AiVoiceStorage GetStorage(IEnumerable<AiVoiceDefinition> voices = null)
        {
            var storage = new AiVoiceStorage();
            foreach (var voice in voices ?? AiVoiceCatalog.All)
            {
                if (IsInstalled(voice)) storage.InstalledCount++;
                storage.ModelBytes += FileBytes(ModelPath(voice));
                storage.PartialBytes += FileBytes(PartialPath(voice));
            }
            return storage;
        }
        internal bool HasLocalFiles(AiVoiceDefinition voice)
        { return File.Exists(ModelPath(voice)) || File.Exists(PartialPath(voice)); }
        private static long FileBytes(string path)
        {
            AiRuntimeInstaller.EnsurePlainPath(path);
            var file = new FileInfo(path);
            return file.Exists ? file.Length : 0;
        }

        internal FileStream AcquireRuntimeLease()
        { return AcquireLock(true); }
        private FileStream AcquireLock(bool reading)
        {
            AiRuntimeInstaller.EnsurePlainPath(DirectoryPath);
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, ".voices.lock");
            AiRuntimeInstaller.EnsurePlainPath(path);
            try { return new FileStream(path, FileMode.OpenOrCreate, reading ? FileAccess.Read : FileAccess.ReadWrite, reading ? FileShare.Read : FileShare.None); }
            catch (IOException) { throw UiText.Error("AiVoiceBusy"); }
        }
        private void NotifyChanged()
        {
            var changed = Changed;
            if (changed != null) changed(DirectoryPath);
        }

        internal Task RemoveAsync(AiVoiceDefinition voice, CancellationToken token)
        {
            return Task.Run(() =>
            {
                try { Remove(voice, token); }
                finally { NotifyChanged(); }
            }, token);
        }
        private void Remove(AiVoiceDefinition voice, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Directory.Exists(DirectoryPath)) return;
            using (AcquireLock(false))
            {
                string model = ModelPath(voice), partial = PartialPath(voice);
                string metadata = Path.Combine(SlotPath(voice), "params.json");
                string root = DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (string path in new[] { model, partial, metadata })
                {
                    if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw UiText.Error("AiArchivePath");
                    AiRuntimeInstaller.EnsurePlainPath(path);
                }
                var existing = ReadMetadata(voice);
                if (existing != null && (!existing.ContainsKey("modelFile") || Convert.ToString(existing["modelFile"]) != voice.FileName))
                    throw UiText.Error("AiVoiceSlotOccupied");
                token.ThrowIfCancellationRequested();
                // Delete only this catalog entry's known files, never the slot tree or engine.
                if (File.Exists(model)) File.Delete(model);
                if (File.Exists(partial)) File.Delete(partial);
                if (File.Exists(metadata)) File.Delete(metadata);
            }
        }
        internal bool IsInstalled(AiVoiceDefinition voice)
        {
            try
            {
                string path = ModelPath(voice);
                if (!File.Exists(path) || new FileInfo(path).Length != voice.Bytes) return false;
                var data = ReadMetadata(voice);
                return data != null && Convert.ToString(data["modelFile"]) == voice.FileName
                    && Convert.ToString(data["voiceChangerType"]) == "RVC"
                    && data.ContainsKey("version") && Convert.ToString(data["version"]) == voice.Version
                    && Convert.ToString(data["modelType"]) == voice.ModelType
                    && Convert.ToInt32(data["samplingRate"]) == voice.SampleRate && Convert.ToBoolean(data["f0"]) == voice.HasPitch;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is FormatException || ex is OverflowException) { return false; }
        }
        private Dictionary<string, object> ReadMetadata(AiVoiceDefinition voice)
        {
            string path = Path.Combine(SlotPath(voice), "params.json");
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 65536) throw UiText.Error("AiVoiceSlotOccupied");
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        }
        internal Task EnsureAsync(AiVoiceDefinition voice, IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                try { await Ensure(voice, progress, token).ConfigureAwait(false); }
                finally { NotifyChanged(); }
            }, token);
        }

        private async Task Ensure(AiVoiceDefinition voice, IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (AcquireLock(false))
            {
                string slot = SlotPath(voice);
                AiRuntimeInstaller.EnsurePlainPath(slot);
                Directory.CreateDirectory(slot);
                AiRuntimeInstaller.EnsurePlainPath(Path.Combine(slot, "params.json"));
                var existing = ReadMetadata(voice);
                if (existing != null && (!existing.ContainsKey("modelFile") || Convert.ToString(existing["modelFile"]) != voice.FileName))
                    throw UiText.Error("AiVoiceSlotOccupied");
                string model = ModelPath(voice);
                AiRuntimeInstaller.EnsurePlainPath(model);
                if (File.Exists(model) && new FileInfo(model).Length == voice.Bytes && Verify(model, voice, progress, token))
                { if (!IsInstalled(voice)) WriteMetadata(voice); return; }
                string partial = PartialPath(voice);
                AiRuntimeInstaller.EnsurePlainPath(partial);
                long offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                if (offset > voice.Bytes) { File.Delete(partial); offset = 0; }
                var drive = new DriveInfo(Path.GetPathRoot(DirectoryPath));
                if (drive.IsReady && drive.AvailableFreeSpace < voice.Bytes - offset + 1024 * 1024)
                    throw UiText.Error("AiVoiceSpace", Math.Ceiling(voice.Bytes / 1000000m));
                await Download(voice.DownloadUri, partial, voice.Bytes, progress, token).ConfigureAwait(false);
                if (!Verify(partial, voice, progress, token)) { File.Delete(partial); throw UiText.Error("AiVoiceCorrupt"); }
                token.ThrowIfCancellationRequested();
                if (File.Exists(model)) File.Replace(partial, model, null);
                else File.Move(partial, model);
                WriteMetadata(voice);
            }
        }
        private static bool Verify(string path, AiVoiceDefinition voice, IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            Report(progress, AiInstallStage.Verify, 0, voice.Bytes);
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
            {
                byte[] buffer = new byte[1024 * 1024];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                { token.ThrowIfCancellationRequested(); hash.TransformBlock(buffer, 0, read, buffer, 0); }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                token.ThrowIfCancellationRequested();
                Report(progress, AiInstallStage.Verify, voice.Bytes, voice.Bytes);
                return BitConverter.ToString(hash.Hash).Replace("-", "").Equals(voice.Sha256, StringComparison.OrdinalIgnoreCase);
            }
        }
        internal static async Task Download(Uri uri, string path, long bytes, IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) throw new ArgumentException("HTTPS required.");
            token.ThrowIfCancellationRequested();
            long offset = File.Exists(path) ? new FileInfo(path).Length : 0;
            Report(progress, AiInstallStage.Download, offset, bytes);
            if (offset == bytes) return;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = "SesStudyosu-Voice-Installer/1.0";
            request.AutomaticDecompression = DecompressionMethods.None;
            if (offset > 0) request.AddRange(offset);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (timeout.Token.Register(request.Abort))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                try
                {
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.PartialContent)
                        {
                            string expected = "bytes " + offset + "-" + (bytes - 1) + "/" + bytes;
                            if (response.Headers[HttpResponseHeader.ContentRange] != expected) throw UiText.Error("AiDownloadInvalid");
                        }
                        else if (response.StatusCode == HttpStatusCode.OK) offset = 0;
                        else throw UiText.Error("AiDownloadInvalid");
                        if (response.ContentLength >= 0 && response.ContentLength != bytes - offset) throw UiText.Error("AiDownloadInvalid");
                        using (var input = response.GetResponseStream())
                        using (var output = new FileStream(path, offset == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None, 131072, true))
                        {
                            byte[] buffer = new byte[131072];
                            var watch = System.Diagnostics.Stopwatch.StartNew();
                            int read;
                            while ((read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                            {
                                if (offset + read > bytes) throw UiText.Error("AiDownloadInvalid");
                                await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
                                offset += read;
                                timeout.CancelAfter(TimeSpan.FromSeconds(90));
                                if (watch.ElapsedMilliseconds > 150) { Report(progress, AiInstallStage.Download, offset, bytes); watch.Restart(); }
                            }
                            if (offset != bytes) throw UiText.Error("AiDownloadIncomplete");
                            output.Flush(true);
                        }
                    }
                    Report(progress, AiInstallStage.Download, bytes, bytes);
                }
                catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
                catch (Exception) when (timeout.IsCancellationRequested) { throw UiText.Error("AiDownloadTimeout"); }
            }
        }
        private void WriteMetadata(AiVoiceDefinition voice)
        {
            var metadata = new
            {
                slotIndex = voice.Slot, voiceChangerType = "RVC", name = voice.Name, description = "", credit = voice.Credit,
                termsOfUseUrl = voice.TermsUrl, iconFile = "", speakers = new Dictionary<string, string> { { "0", "target" } },
                modelFile = voice.FileName, indexFile = "", defaultTune = voice.HasPitch ? 12 : 0, defaultIndexRatio = 0,
                defaultProtect = 0.5, isONNX = voice.IsOnnx, modelType = voice.ModelType, samplingRate = voice.SampleRate,
                f0 = voice.HasPitch, embChannels = 768, embOutputLayer = 12, useFinalProj = false, deprecated = false,
                embedder = "hubert_base", sampleId = voice.Id, version = voice.Version
            };
            string target = Path.Combine(SlotPath(voice), "params.json");
            string temporary = Path.Combine(SlotPath(voice), ".params-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(metadata), new UTF8Encoding(false));
                if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void Report(IProgress<AiInstallProgress> progress, AiInstallStage stage, long completed, long total)
        { if (progress != null) progress.Report(new AiInstallProgress(stage, completed, total)); }
    }
}
