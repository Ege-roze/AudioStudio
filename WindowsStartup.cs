using System;
using System.IO;
using Microsoft.Win32;

namespace ses_uyg
{
    internal interface IStartupRegistration
    {
        string ReadCommand();
        void WriteCommand(string command);
    }

    internal sealed class WindowsStartupRegistration : IStartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SesStudyosu";

        public string ReadCommand()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                return key == null ? null : key.GetValue(ValueName) as string;
        }

        public void WriteCommand(string command)
        {
            // Only this user's app entry is changed; no administrator access is needed.
            if (command == null)
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (key != null) key.DeleteValue(ValueName, false);
            }
            else
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                    key.SetValue(ValueName, command, RegistryValueKind.String);
            }
        }

        internal static string CommandFor(string executable)
        {
            if (string.IsNullOrWhiteSpace(executable) || executable.IndexOf('"') >= 0)
                throw new ArgumentException("Invalid executable path.");
            return "\"" + Path.GetFullPath(executable) + "\"";
        }
    }
}
