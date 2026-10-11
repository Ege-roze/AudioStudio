using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ses_uyg
{
    internal static class VbCableInstaller
    {
        private const string PackageHash = "B950E39F01AF1D04EA623C8F6D8EB9B6EA5C477C637295FABF20631C85116BFB";

        internal static string PrepareInstaller(string directory, bool is64Bit)
        {
            using (var package = Assembly.GetExecutingAssembly().GetManifestResourceStream("ses_uyg.VBCABLE_Driver_Pack45.zip"))
            {
                if (package == null) throw UiText.Error("PackageMissing");
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(package)).Replace("-", "");
                    if (hash != PackageHash) throw UiText.Error("PackageInvalid");
                }
                package.Position = 0;
                Directory.CreateDirectory(directory);
                using (var archive = new ZipArchive(package, ZipArchiveMode.Read))
                {
                    string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    foreach (var entry in archive.Entries)
                    {
                        string destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
                        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            throw UiText.Error("PackagePathInvalid");
                        if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(destination);
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            entry.ExtractToFile(destination);
                        }
                    }
                }
            }
            string installer = Path.Combine(directory, is64Bit ? "VBCABLE_Setup_x64.exe" : "VBCABLE_Setup.exe");
            if (!File.Exists(installer)) throw UiText.Error("InstallerMissing", installer);
            return installer;
        }

        internal static async Task<int> RunAsync()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ses-uyg-vbcable-" + Guid.NewGuid().ToString("N"));
            try
            {
                string installer = await Task.Run(() => PrepareInstaller(directory, Environment.Is64BitOperatingSystem));
                // Kurucu görünür çalışır; sürücü kurulumu ve UAC onayı kullanıcıya aittir.
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = installer,
                    WorkingDirectory = directory,
                    UseShellExecute = true,
                    Verb = "runas"
                }))
                {
                    if (process == null) throw UiText.Error("InstallerOpenError");
                    await Task.Run(() => process.WaitForExit());
                    return process.ExitCode;
                }
            }
            finally
            {
                // Yalnızca bu çağrının oluşturduğu geçici klasörü temizle.
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
