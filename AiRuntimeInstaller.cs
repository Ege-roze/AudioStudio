using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ses_uyg
{
    internal enum AiInstallStage { Download, Verify, Extract }

    internal sealed class AiInstallProgress
    {
        internal AiInstallStage Stage { get; private set; }
        internal long Completed { get; private set; }
        internal long Total { get; private set; }
        internal int Percent { get { return Total <= 0 ? 0 : (int)Math.Min(100, Completed * 100 / Total); } }
        internal AiInstallProgress(AiInstallStage stage, long completed, long total)
        { Stage = stage; Completed = completed; Total = total; }
    }

    // Native, per-user installation. No shell, Python, driver or elevation is needed.
    internal sealed class AiRuntimeInstaller
    {
        internal const long PackageBytes = 3308026202;
        private const long UnpackedBytes = 6101760303;
        private const string PackageHash = "D3B3458A3E7F9944336F446477D10A0987CA12FC97B2E6F5AB0AA75E50577CEB";
        private const string PackageUrl = "https://huggingface.co/wok000/vcclient000/resolve/main/MMVCServerSIO_win_onnxgpu-cuda_v.1.5.3.18a.zip";
        internal static string UserInstallDirectory
        { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SesStudyosu", "AiRuntime"); } }

        private readonly string destination;
        private readonly Uri url;
        private readonly long packageBytes;
        private readonly string packageHash;
        private readonly long reserveBytes;

        internal AiRuntimeInstaller() : this(UserInstallDirectory, new Uri(PackageUrl), PackageBytes, PackageHash, UnpackedBytes + 2L * 1024 * 1024 * 1024) { }

        // Explicit destination and pinned package make isolated installation tests possible.
        internal AiRuntimeInstaller(string destination, Uri url, long packageBytes, string packageHash, long reserveBytes = 0)
        {
            if (url.Scheme != "https" && !(url.Scheme == "http" && url.IsLoopback)) throw new ArgumentException("HTTPS required.");
            if (packageBytes <= 0 || packageHash.Length != 64) throw new ArgumentException("Invalid package manifest.");
            this.destination = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
            this.url = url;
            this.packageBytes = packageBytes;
            this.packageHash = packageHash;
            this.reserveBytes = reserveBytes;
        }

        internal static bool IsInstalled(string root)
        {
            return File.Exists(Path.Combine(root, "installed.txt")) &&
                File.Exists(Path.Combine(root, "MMVCServerSIO", "MMVCServerSIO.exe")) &&
                File.Exists(Path.Combine(root, "MMVCServerSIO", "python310.dll"));
        }

        internal Task InstallAsync(IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            return Task.Run(() => Install(progress, token), token);
        }

        private async Task Install(IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Environment.Is64BitOperatingSystem) throw UiText.Error("AiWindows64Required");
            EnsurePlainPath(destination);
            Directory.CreateDirectory(destination);
            EnsurePlainPath(Path.Combine(destination, ".install.lock"));
            FileStream installLock;
            try { installLock = new FileStream(Path.Combine(destination, ".install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw UiText.Error("AiInstallBusy"); }
            using (installLock)
            {
                if (IsInstalled(destination)) return;
                string archive = Path.Combine(destination, "vcclient-1.5.3.18a.zip.part");
                EnsurePlainPath(archive);
                long existing = File.Exists(archive) ? new FileInfo(archive).Length : 0;
                if (existing > packageBytes) { File.Delete(archive); existing = 0; }
                CheckSpace(packageBytes - existing + reserveBytes);
                await Download(archive, progress, token).ConfigureAwait(false);
                Verify(archive, progress, token);
                string staging = Path.Combine(destination, ".staging");
                try
                {
                    DeleteOwnedTree(staging);
                    Directory.CreateDirectory(staging);
                    Extract(archive, staging, progress, token);
                    token.ThrowIfCancellationRequested();
                    Commit(staging);
                }
                finally { DeleteOwnedTree(staging); }
                // The installed engine is complete; this cache is no longer needed.
                try { File.Delete(archive); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private async Task Download(string path, IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            long offset = File.Exists(path) ? new FileInfo(path).Length : 0;
            Report(progress, AiInstallStage.Download, offset, packageBytes);
            if (offset == packageBytes) return;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.None;
            request.UserAgent = "SesStudyosu-AI-Installer/1.0";
            if (offset > 0) request.AddRange(offset);
            using (var idle = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (idle.Token.Register(request.Abort))
            {
                idle.CancelAfter(TimeSpan.FromSeconds(90));
                try
                {
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.PartialContent)
                        {
                            string range = response.Headers[HttpResponseHeader.ContentRange];
                            string expected = "bytes " + offset + "-" + (packageBytes - 1) + "/" + packageBytes;
                            if (range != expected) throw UiText.Error("AiDownloadInvalid");
                        }
                        else if (response.StatusCode == HttpStatusCode.OK) offset = 0; // Server ignored Range: safely restart.
                        else throw UiText.Error("AiDownloadInvalid");
                        if (response.ContentLength >= 0 && response.ContentLength != packageBytes - offset) throw UiText.Error("AiDownloadInvalid");
                        using (var input = response.GetResponseStream())
                        using (var output = new FileStream(path, offset == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None, 131072, true))
                        {
                            var buffer = new byte[131072];
                            var reporting = Stopwatch.StartNew();
                            int read;
                            while ((read = await input.ReadAsync(buffer, 0, buffer.Length, idle.Token).ConfigureAwait(false)) > 0)
                            {
                                if (offset + read > packageBytes) throw UiText.Error("AiDownloadInvalid");
                                await output.WriteAsync(buffer, 0, read, idle.Token).ConfigureAwait(false);
                                offset += read;
                                idle.CancelAfter(TimeSpan.FromSeconds(90));
                                if (reporting.ElapsedMilliseconds >= 150)
                                {
                                    Report(progress, AiInstallStage.Download, offset, packageBytes);
                                    reporting.Restart();
                                }
                            }
                            if (offset != packageBytes) throw UiText.Error("AiDownloadIncomplete");
                            output.Flush(true);
                        }
                    }
                    Report(progress, AiInstallStage.Download, offset, packageBytes);
                }
                catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
                catch (Exception) when (idle.IsCancellationRequested) { throw UiText.Error("AiDownloadTimeout"); }
            }
        }

        private void Verify(string path, IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            Report(progress, AiInstallStage.Verify, 0, packageBytes);
            bool valid;
            using (var input = File.OpenRead(path))
            using (var hash = SHA256.Create())
            {
                var buffer = new byte[1024 * 1024];
                var reporting = Stopwatch.StartNew();
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    hash.TransformBlock(buffer, 0, read, buffer, 0);
                    if (reporting.ElapsedMilliseconds >= 150)
                    {
                        Report(progress, AiInstallStage.Verify, input.Position, packageBytes);
                        reporting.Restart();
                    }
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                valid = string.Equals(BitConverter.ToString(hash.Hash).Replace("-", ""), packageHash, StringComparison.OrdinalIgnoreCase);
            }
            token.ThrowIfCancellationRequested();
            if (!valid)
            {
                File.Delete(path); // A corrupt complete cache must not poison every retry.
                throw UiText.Error("AiPackageCorrupt");
            }
            Report(progress, AiInstallStage.Verify, packageBytes, packageBytes);
        }

        private void Extract(string archivePath, string staging, IProgress<AiInstallProgress> progress, CancellationToken token)
        {
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                long total = zip.Entries.Sum(entry => entry.Length);
                CheckSpace(total);
                long completed = 0;
                var buffer = new byte[131072];
                var reporting = Stopwatch.StartNew();
                Report(progress, AiInstallStage.Extract, 0, total);
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    if (Path.IsPathRooted(relative) || relative.Contains(":")) throw UiText.Error("AiArchivePath");
                    string target = Path.GetFullPath(Path.Combine(staging, relative));
                    if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw UiText.Error("AiArchivePath");
                    EnsurePlainPath(target);
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var input = entry.Open())
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            output.Write(buffer, 0, read);
                            completed += read;
                            if (reporting.ElapsedMilliseconds >= 150)
                            {
                                Report(progress, AiInstallStage.Extract, completed, total);
                                reporting.Restart();
                            }
                        }
                    }
                }
                if (!File.Exists(Path.Combine(staging, "MMVCServerSIO", "MMVCServerSIO.exe")) ||
                    !File.Exists(Path.Combine(staging, "MMVCServerSIO", "python310.dll"))) throw UiText.Error("AiArchiveInvalid");
                Report(progress, AiInstallStage.Extract, total, total);
            }
        }

        private void Commit(string staging)
        {
            string target = Path.Combine(destination, "MMVCServerSIO");
            string previous = Path.Combine(destination, ".previous-" + Guid.NewGuid().ToString("N"));
            string marker = Path.Combine(destination, "installed.txt");
            string temporaryMarker = Path.Combine(destination, ".installed.tmp");
            EnsurePlainPath(target);
            EnsurePlainPath(marker);
            EnsurePlainPath(temporaryMarker);
            bool movedPrevious = false;
            bool movedNew = false;
            try
            {
                if (Directory.Exists(target)) { Directory.Move(target, previous); movedPrevious = true; }
                Directory.Move(Path.Combine(staging, "MMVCServerSIO"), target);
                movedNew = true;
                File.WriteAllText(temporaryMarker, "VCClient 1.5.3.18a CUDA - SHA256 " + packageHash);
                if (File.Exists(marker)) File.Replace(temporaryMarker, marker, null);
                else File.Move(temporaryMarker, marker);
            }
            catch
            {
                if (movedNew) DeleteOwnedTree(target);
                if (movedPrevious) Directory.Move(previous, target);
                throw;
            }
            finally { if (File.Exists(temporaryMarker)) File.Delete(temporaryMarker); }
            // Keep any pre-existing incomplete installation (and user models) as a backup.
        }

        private void CheckSpace(long needed)
        {
            var drive = new DriveInfo(Path.GetPathRoot(destination));
            if (drive.IsReady && drive.AvailableFreeSpace < needed)
                throw UiText.Error("AiSpaceNeeded", Math.Ceiling(needed / 1000000000.0));
        }

        internal static void EnsurePlainPath(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw UiText.Error("AiArchivePath");
        }

        private void DeleteOwnedTree(string path)
        {
            path = Path.GetFullPath(path);
            if (!path.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw UiText.Error("AiArchivePath");
            EnsurePlainPath(path);
            if (!Directory.Exists(path)) return;
            foreach (string file in Directory.GetFiles(path)) { EnsurePlainPath(file); File.Delete(file); }
            foreach (string child in Directory.GetDirectories(path)) DeleteOwnedTree(child);
            Directory.Delete(path);
        }

        private static void Report(IProgress<AiInstallProgress> progress, AiInstallStage stage, long completed, long total)
        { if (progress != null) progress.Report(new AiInstallProgress(stage, completed, total)); }
    }
}
