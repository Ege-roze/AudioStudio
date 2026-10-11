using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ses_uyg
{
    internal sealed class ApplicationOptions
    {
        public bool CloseToTray { get; set; } = true;
        public bool StartInTray { get; set; }
        public string MicrophoneName { get; set; }
        public string OutputName { get; set; }
        public string AiVoiceId { get; set; }

        internal ApplicationOptions Copy() { return (ApplicationOptions)MemberwiseClone(); }
        internal void Validate()
        {
            if ((MicrophoneName != null && MicrophoneName.Length > 1024)
                || (OutputName != null && OutputName.Length > 1024)
                || (AiVoiceId != null && AiVoiceId.Length > 128))
                throw new InvalidDataException("Invalid device name.");
        }
    }

    internal sealed class ApplicationSettingsStore
    {
        private readonly string directory;
        internal static string DefaultDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SesStudyosu"); } }
        internal string SettingsPath { get { return Path.Combine(directory, "settings.json"); } }
        internal ApplicationSettingsStore(string directory) { this.directory = Path.GetFullPath(directory); }

        internal ApplicationOptions Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new ApplicationOptions();
                if (new FileInfo(SettingsPath).Length > 16384) throw new InvalidDataException();
                var options = new JavaScriptSerializer().Deserialize<ApplicationOptions>(File.ReadAllText(SettingsPath));
                if (options == null) throw new InvalidDataException();
                options.Validate();
                return options;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException
                || ex is InvalidOperationException || ex is FormatException || ex is OverflowException)
            { return new ApplicationOptions(); }
        }

        internal ApplicationOptions Save(ApplicationOptions value, IStartupRegistration startup = null,
            bool? startWithWindows = null, string executable = null)
        {
            var options = value.Copy();
            options.Validate();
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            string previousCommand = null;
            bool startupChanged = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] json = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(options));
                    stream.Write(json, 0, json.Length);
                    stream.Flush(true);
                }
                if (startWithWindows.HasValue)
                {
                    previousCommand = startup.ReadCommand();
                    string desired = startWithWindows.Value ? WindowsStartupRegistration.CommandFor(executable) : null;
                    if (!string.Equals(previousCommand, desired, StringComparison.OrdinalIgnoreCase))
                    {
                        startupChanged = true;
                        startup.WriteCommand(desired);
                    }
                }
                if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
                else File.Move(temporary, SettingsPath);
                return options;
            }
            catch (Exception saveError)
            {
                if (startupChanged)
                {
                    try { startup.WriteCommand(previousCommand); }
                    catch (Exception rollbackError) { throw new AggregateException(saveError, rollbackError); }
                }
                throw;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
