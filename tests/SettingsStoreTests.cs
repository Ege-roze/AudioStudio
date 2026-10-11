using System;
using System.IO;
using ses_uyg;

internal static class SettingsStoreTests
{
    private static int checks;
    private sealed class Startup : IStartupRegistration
    {
        internal string Command;
        internal bool FailNext;
        public string ReadCommand() { return Command; }
        public void WriteCommand(string value)
        {
            if (FailNext) { FailNext = false; throw new UnauthorizedAccessException("Startup write rejected"); }
            Command = value;
        }
    }

    private static int Main()
    {
        try
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-tests-" + Guid.NewGuid().ToString("N"));
            var store = new ApplicationSettingsStore(directory);
            var options = store.Load();
            Check(options.CloseToTray && !options.StartInTray && !Directory.Exists(directory), "Defaults preserve close-to-tray without writing files or enabling startup");
            options.CloseToTray = false;
            options.StartInTray = true;
            options.MicrophoneName = "Realtek Mikrofon";
            options.OutputName = "CABLE Input";
            var startup = new Startup();
            string executable = Path.Combine(directory, "App with spaces", "ses uyg.exe");
            store.Save(options, startup, true, executable);
            var loaded = new ApplicationSettingsStore(directory).Load();
            Check(!loaded.CloseToTray && loaded.StartInTray && loaded.MicrophoneName == options.MicrophoneName
                && loaded.OutputName == options.OutputName, "Device and independent lifecycle preferences survive a new store instance");
            Check(startup.Command == "\"" + executable + "\"", "Windows startup quotes the complete executable path");
            store.Save(options, startup, false, executable);
            Check(startup.Command == null, "Disabling Windows startup removes only the application's registration");
            string original = File.ReadAllText(store.SettingsPath);
            options.CloseToTray = true;
            startup.FailNext = true;
            bool failed = false;
            try { store.Save(options, startup, true, executable); }
            catch (UnauthorizedAccessException) { failed = true; }
            Check(failed && File.ReadAllText(store.SettingsPath) == original && startup.Command == null,
                "A rejected startup change cannot publish new preferences");
            failed = false;
            using (new FileStream(store.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { store.Save(options, startup, true, executable); }
                catch (IOException) { failed = true; }
            }
            Check(failed && startup.Command == null && File.ReadAllText(store.SettingsPath) == original,
                "A settings write failure rolls back the startup registration");
            Check(Directory.GetFiles(directory, ".settings-*.tmp").Length == 0, "Failed saves remove their temporary settings files");
            store.Save(options, startup, true, executable);
            Check(store.Load().CloseToTray && startup.Command != null, "Save can be retried after failure");
            store.Save(options);
            Check(startup.Command != null, "Saving without a startup change leaves its registration untouched");
            File.WriteAllText(store.SettingsPath, "not-json");
            Check(store.Load().CloseToTray && !store.Load().StartInTray, "Corrupt settings safely fall back to visible startup and close-to-tray");
            File.WriteAllText(store.SettingsPath, "{\"MicrophoneName\":\"" + new string('x', 1025) + "\"}");
            Check(store.Load().MicrophoneName == null, "Invalid settings are rejected");
            File.WriteAllText(store.SettingsPath, new string('x', 20000));
            Check(!store.Load().StartInTray, "Oversized settings cannot prevent startup");
            Console.WriteLine("PASS: " + checks + " settings store checks. Startup registration was simulated; the Windows registry was not changed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        checks++;
        Console.WriteLine("PASS: " + message);
    }
}
