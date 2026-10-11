using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ses_uyg
{
    internal sealed class AiRuntime : IDisposable
    {
        private Process process;
        private FileStream modelLease;
        private string directory;
        private string certificatePath;
        internal AiVoiceClient Client { get; private set; }

        internal static string FindRuntime()
        {
            return FindRuntime(Application.StartupPath, AiRuntimeInstaller.UserInstallDirectory);
        }

        internal static string FindRuntime(string startupPath, string userInstallDirectory)
        {
            if (AiRuntimeInstaller.IsInstalled(userInstallDirectory))
                return Path.Combine(userInstallDirectory, "MMVCServerSIO", "MMVCServerSIO.exe");
            string root = startupPath;
            for (int i = 0; i < 4 && root != null; i++, root = Path.GetDirectoryName(root))
            {
                string runtime = Path.Combine(root, "AiRuntime");
                if (AiRuntimeInstaller.IsInstalled(runtime))
                    return Path.Combine(runtime, "MMVCServerSIO", "MMVCServerSIO.exe");
            }
            return null;
        }

        internal Task Start(CancellationToken token)
        { return Start(AiVoiceStore.Default().DirectoryPath, token); }

        internal async Task Start(string modelDirectory, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (process != null && !process.HasExited) return;
            Dispose();
            certificatePath = null;
            string executable = FindRuntime();
            if (executable == null) throw UiText.Error("AiRuntimeMissing");
            directory = Path.GetDirectoryName(executable);
            // This integration owns its model directory, independently of VCClient's UI.
            modelLease = new AiVoiceStore(modelDirectory).AcquireRuntimeLease();
            // HTTPS mode runs the server without launching VCClient's separate window.
            var start = new ProcessStartInfo(executable, "--https true --httpsSelfSigned true --test_connect 127.0.0.1 --model_dir \"" + Path.GetFullPath(modelDirectory) + "\" -p 18991")
            {
                WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            start.EnvironmentVariables["PYTHONUTF8"] = "1";
            string temporary = Path.Combine(directory, "temp");
            Directory.CreateDirectory(temporary);
            start.EnvironmentVariables["TMPDIR"] = temporary;
            start.EnvironmentVariables["TEMP"] = temporary;
            start.EnvironmentVariables["TMP"] = temporary;
            start.StandardOutputEncoding = System.Text.Encoding.UTF8;
            start.StandardErrorEncoding = System.Text.Encoding.UTF8;
            process = Process.Start(start);
            var owned = process;
            // Drain both pipes; retain the most recent boot output in a bounded log.
            var logSync = new object();
            string logPath = Path.Combine(directory, "ses-ai.log");
            Action<string> log = line =>
            {
                if (line == null) return;
                int certOffset = line.IndexOf(", cert:", StringComparison.Ordinal);
                if (certOffset >= 0)
                {
                    string candidate = line.Substring(certOffset + 7).Trim();
                    if (Path.GetFullPath(candidate).StartsWith(temporary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        certificatePath = candidate;
                }
                lock (logSync)
                {
                    try
                    {
                        if (File.Exists(logPath) && new FileInfo(logPath).Length > 1024 * 1024) File.WriteAllText(logPath, "");
                        File.AppendAllText(logPath, line + Environment.NewLine);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            };
            process.OutputDataReceived += (s, e) => log(e.Data);
            process.ErrorDataReceived += (s, e) => log(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var client = new AiVoiceClient(new Uri("https://127.0.0.1:18991/"), ReadCertificate);
            Client = client;
            var watch = Stopwatch.StartNew();
            string lastProbeError = null;
            while (watch.Elapsed < TimeSpan.FromMinutes(10))
            {
                token.ThrowIfCancellationRequested();
                if (owned.HasExited) throw UiText.Error("AiBootFailed", logPath);
                try
                {
                    using (var probe = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        probe.CancelAfter(1500);
                        await client.Info(probe.Token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        return;
                    }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    if (ex.Message != lastProbeError) { lastProbeError = ex.Message; log("Connection: " + ex); }
                }
                await Task.Delay(500, token).ConfigureAwait(false);
            }
            throw UiText.Error("AiBootFailed", logPath);
        }

        private X509Certificate2 ReadCertificate()
        {
            string path = certificatePath;
            if (path == null || !File.Exists(path)) return null;
            try { return new X509Certificate2(path); }
            catch (System.Security.Cryptography.CryptographicException) { return null; }
        }

        public void Dispose()
        {
            var owned = process;
            process = null;
            Client = null;
            if (owned == null) { if (modelLease != null) { modelLease.Dispose(); modelLease = null; } return; }
            try
            {
                if (!owned.HasExited)
                {
                    using (var kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + owned.Id + " /T /F")
                    { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                        if (kill != null) kill.WaitForExit(3000);
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally
            {
                owned.Dispose();
                if (modelLease != null) { modelLease.Dispose(); modelLease = null; }
            }
        }
    }
}
