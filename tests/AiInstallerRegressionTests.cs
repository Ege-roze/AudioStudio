using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ses_uyg;

// Tiny local HTTP fixtures exercise the real downloader/extractor. No AI is launched.
internal static class AiInstallerRegressionTests
{
    private const string CacheName = "vcclient-1.5.3.18a.zip.part";
    private static readonly string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installer-tests-" + Guid.NewGuid().ToString("N"));
    private static int checks;

    private static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + checks + " AI installation checks."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // Only remove this run's freshly created fixture directory.
            string parent = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static async Task Run()
    {
        Directory.CreateDirectory(root);
        byte[] package = Package();
        string hash = Hash(package);
        string fresh = Folder("fresh");
        var stages = new List<AiInstallStage>();
        using (var server = new Server(package))
        {
            await Installer(fresh, server, package, hash).InstallAsync(new ProgressSink(p => stages.Add(p.Stage)), CancellationToken.None);
            Check(AiRuntimeInstaller.IsInstalled(fresh), "A fresh computer gets a complete engine and installation marker");
            Check(File.ReadAllText(Path.Combine(fresh, "MMVCServerSIO", "MMVCServerSIO.exe")) == "fixture engine", "Installed files match the verified package");
            Check(stages.Contains(AiInstallStage.Download) && stages.Contains(AiInstallStage.Verify) && stages.Contains(AiInstallStage.Extract), "Progress covers download, verification and extraction");
            Check(!File.Exists(Path.Combine(fresh, CacheName)), "Successful installation removes the download cache");
        }
        // The server is deliberately unavailable: an existing complete install must need no download.
        await new AiRuntimeInstaller(fresh, new Uri("http://127.0.0.1:1/"), package.Length, hash).InstallAsync(null, CancellationToken.None);
        Check(AiRuntimeInstaller.IsInstalled(fresh), "Reusing an existing installation works offline");

        string resumed = Folder("resume");
        File.WriteAllBytes(Path.Combine(resumed, CacheName), package.Take(12345).ToArray());
        using (var server = new Server(package))
        {
            await Installer(resumed, server, package, hash).InstallAsync(null, CancellationToken.None);
            Check(server.Range == "bytes=12345-" && AiRuntimeInstaller.IsInstalled(resumed), "An interrupted download resumes at the saved byte offset");
        }
        string ignored = Folder("range-ignored");
        File.WriteAllBytes(Path.Combine(ignored, CacheName), package.Take(500).ToArray());
        using (var server = new Server(package, ignoreRange: true))
        {
            await Installer(ignored, server, package, hash).InstallAsync(null, CancellationToken.None);
            Check(AiRuntimeInstaller.IsInstalled(ignored), "A server that ignores Range restarts the file without appending duplicate bytes");
        }

        string interrupted = Folder("cancel-download");
        using (var cancel = new CancellationTokenSource())
        using (var server = new Server(package, stall: true))
        {
            Task installation = Installer(interrupted, server, package, hash).InstallAsync(null, cancel.Token);
            await Within(server.SentFirstChunk.Task);
            await Task.Delay(150);
            cancel.Cancel();
            await Fails<OperationCanceledException>(() => Within(installation));
            long saved = new FileInfo(Path.Combine(interrupted, CacheName)).Length;
            Check(saved > 0 && saved < package.Length && !AiRuntimeInstaller.IsInstalled(interrupted), "Cancelling a live download keeps only the partial cache and no installed marker");
        }
        using (var server = new Server(package))
        {
            await Installer(interrupted, server, package, hash).InstallAsync(null, CancellationToken.None);
            Check(server.Range != null && AiRuntimeInstaller.IsInstalled(interrupted), "Enabling again completes a previously cancelled download");
        }

        string corrupt = Folder("corrupt");
        File.WriteAllBytes(Path.Combine(corrupt, CacheName), new byte[package.Length]);
        await Fails<InvalidOperationException>(() => new AiRuntimeInstaller(corrupt, new Uri("http://127.0.0.1:1/"), package.Length, hash).InstallAsync(null, CancellationToken.None));
        Check(!File.Exists(Path.Combine(corrupt, CacheName)) && !AiRuntimeInstaller.IsInstalled(corrupt), "Hash failure removes the corrupt cache and never installs it");
        using (var server = new Server(package))
        {
            await Installer(corrupt, server, package, hash).InstallAsync(null, CancellationToken.None);
            Check(AiRuntimeInstaller.IsInstalled(corrupt), "A hash failure can be retried successfully");
        }

        string truncated = Folder("truncated");
        using (var server = new Server(package, truncate: true))
        {
            await Fails<Exception>(() => Installer(truncated, server, package, hash).InstallAsync(null, CancellationToken.None));
            Check(!AiRuntimeInstaller.IsInstalled(truncated), "A dropped connection cannot create an installed marker");
        }
        string badRange = Folder("bad-range");
        File.WriteAllBytes(Path.Combine(badRange, CacheName), package.Take(500).ToArray());
        using (var server = new Server(package, badRange: true))
        {
            await Fails<InvalidOperationException>(() => Installer(badRange, server, package, hash).InstallAsync(null, CancellationToken.None));
            Check(new FileInfo(Path.Combine(badRange, CacheName)).Length == 500, "An invalid Content-Range cannot corrupt an existing partial download");
        }

        await RejectPackage("traversal", Package("../escaped.txt"), "Archive traversal stays inside the installer directory");
        Check(!File.Exists(Path.Combine(root, "traversal", "escaped.txt")), "An archive cannot write outside its staging directory");
        await RejectPackage("missing-engine", Package(includeEngine: false), "An incomplete package is not marked installed");
        string extraction = Folder("cancel-extract");
        File.WriteAllBytes(Path.Combine(extraction, CacheName), package);
        using (var cancel = new CancellationTokenSource())
        {
            var progress = new ProgressSink(p => { if (p.Stage == AiInstallStage.Extract) cancel.Cancel(); });
            await Fails<OperationCanceledException>(() => new AiRuntimeInstaller(extraction, new Uri("http://127.0.0.1:1/"), package.Length, hash).InstallAsync(progress, cancel.Token));
            Check(!AiRuntimeInstaller.IsInstalled(extraction) && !Directory.Exists(Path.Combine(extraction, ".staging")) && File.Exists(Path.Combine(extraction, CacheName)), "Cancelling extraction removes staging and preserves the verified download for retry");
        }
        string locked = Folder("locked");
        using (File.Open(Path.Combine(locked, ".install.lock"), FileMode.Create, FileAccess.Write, FileShare.None))
            await Fails<InvalidOperationException>(() => new AiRuntimeInstaller(locked, new Uri("http://127.0.0.1:1/"), package.Length, hash).InstallAsync(null, CancellationToken.None));
        Check(!AiRuntimeInstaller.IsInstalled(locked), "Concurrent installation is refused before any engine is published");

        string application = Folder("app");
        string localRuntime = Path.Combine(application, "AiRuntime");
        Directory.CreateDirectory(Path.Combine(localRuntime, "MMVCServerSIO"));
        File.WriteAllText(Path.Combine(localRuntime, "installed.txt"), "legacy marker");
        File.WriteAllText(Path.Combine(localRuntime, "MMVCServerSIO", "MMVCServerSIO.exe"), "legacy engine");
        Check(AiRuntime.FindRuntime(application, Folder("absent-user")) == null, "Incomplete engines are skipped during discovery");
        File.WriteAllText(Path.Combine(localRuntime, "MMVCServerSIO", "python310.dll"), "legacy python");
        Check(AiRuntime.FindRuntime(Path.Combine(application, "bin", "Debug"), Folder("absent-user-2")).StartsWith(localRuntime), "The developer's existing project engine remains discoverable");
        Check(AiRuntime.FindRuntime(application, fresh).StartsWith(fresh), "The application finds and prioritizes its per-user installation");
        foreach (UiLanguage language in Enum.GetValues(typeof(UiLanguage)))
        {
            UiText.Language = language;
            foreach (string key in new[] { "AiDownloading", "AiVerifying", "AiExtracting", "AiCancelling", "AiCancelled", "AiInstallBusy", "AiDownloadInvalid", "AiDownloadIncomplete", "AiDownloadTimeout", "AiPackageCorrupt", "AiArchivePath", "AiArchiveInvalid", "AiSpaceNeeded", "AiWindows64Required" })
                Check(!string.IsNullOrWhiteSpace(UiText.Get(key, 50, 100m, 200m)), language + " setup message: " + key);
        }
    }

    private static async Task RejectPackage(string name, byte[] package, string description)
    {
        string folder = Folder(name);
        File.WriteAllBytes(Path.Combine(folder, CacheName), package);
        await Fails<InvalidOperationException>(() => new AiRuntimeInstaller(folder, new Uri("http://127.0.0.1:1/"), package.Length, Hash(package)).InstallAsync(null, CancellationToken.None));
        Check(!AiRuntimeInstaller.IsInstalled(folder) && !Directory.Exists(Path.Combine(folder, ".staging")), description);
    }

    private static AiRuntimeInstaller Installer(string folder, Server server, byte[] package, string hash)
    { return new AiRuntimeInstaller(folder, server.Address, package.Length, hash); }
    private static string Folder(string name) { string path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
    private static string Hash(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""); }
    private static void Check(bool condition, string description) { if (!condition) throw new Exception(description); checks++; Console.WriteLine("PASS: " + description); }
    private static async Task Within(Task task) { if (await Task.WhenAny(task, Task.Delay(10000)) != task) throw new TimeoutException("Fixture timed out."); await task; }
    private static async Task Fails<T>(Func<Task> action) where T : Exception
    {
        try { await Within(action()); }
        catch (T error) { if (error is TimeoutException) throw; return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static byte[] Package(string extraPath = null, bool includeEngine = true)
    {
        using (var memory = new MemoryStream())
        {
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                if (includeEngine)
                {
                    using (var writer = new StreamWriter(zip.CreateEntry("MMVCServerSIO/MMVCServerSIO.exe").Open())) writer.Write("fixture engine");
                    using (var writer = new StreamWriter(zip.CreateEntry("MMVCServerSIO/python310.dll").Open())) writer.Write("fixture python");
                }
                var data = new byte[512 * 1024];
                new Random(42).NextBytes(data);
                using (var output = zip.CreateEntry(extraPath ?? "MMVCServerSIO/payload.bin", CompressionLevel.NoCompression).Open()) output.Write(data, 0, data.Length);
            }
            return memory.ToArray();
        }
    }
    private sealed class ProgressSink : IProgress<AiInstallProgress>
    {
        private readonly Action<AiInstallProgress> action;
        internal ProgressSink(Action<AiInstallProgress> action) { this.action = action; }
        public void Report(AiInstallProgress value) { action(value); }
    }
    private sealed class Server : IDisposable
    {
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Task worker;
        internal readonly TaskCompletionSource<bool> SentFirstChunk = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Uri Address { get; private set; }
        internal string Range { get; private set; }
        internal Server(byte[] package, bool ignoreRange = false, bool stall = false, bool truncate = false, bool badRange = false)
        {
            listener.Start();
            Address = new Uri("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/engine.zip");
            worker = Task.Run(async () =>
            {
                try
                {
                    using (var client = await listener.AcceptTcpClientAsync())
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                    {
                        string line;
                        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                            if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) Range = line.Substring(6).Trim();
                        int offset = Range == null || ignoreRange ? 0 : int.Parse(Range.Substring(6).TrimEnd('-'));
                        bool partial = Range != null && !ignoreRange;
                        string headers = "HTTP/1.1 " + (partial ? "206 Partial Content" : "200 OK") + "\r\nContent-Length: " + (package.Length - offset) + "\r\nConnection: close\r\n";
                        if (partial) headers += "Content-Range: bytes " + (badRange ? offset + 1 : offset) + "-" + (package.Length - 1) + "/" + package.Length + "\r\n";
                        byte[] headerBytes = Encoding.ASCII.GetBytes(headers + "\r\n");
                        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                        int length = stall || truncate ? 32768 : package.Length - offset;
                        await stream.WriteAsync(package, offset, length);
                        await stream.FlushAsync();
                        SentFirstChunk.TrySetResult(true);
                        if (stall) await Task.Delay(Timeout.Infinite, stop.Token);
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
                catch (SocketException) when (stop.IsCancellationRequested) { }
                catch (IOException) { } // Rejected responses close the client before the body finishes.
            });
        }
        public void Dispose() { stop.Cancel(); listener.Stop(); worker.GetAwaiter().GetResult(); stop.Dispose(); }
    }
}
