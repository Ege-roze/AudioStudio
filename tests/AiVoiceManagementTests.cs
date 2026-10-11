using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class AiVoiceManagementTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, Instance).Invoke(target, args); }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var storeType = assembly.GetType("ses_uyg.AiVoiceStore", true);
            var pickerType = assembly.GetType("ses_uyg.AiVoicePicker", true);
            var subscriptions = storeType.GetField("Changed", Static);
            var before = subscriptions.GetValue(null) as Delegate;
            int initial = before == null ? 0 : before.GetInvocationList().Length;
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice-ui-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, ".voice-0.part"), new byte[256000]);
            var store = Activator.CreateInstance(storeType, Instance, null, new object[] { root }, null);
            using (var form = new Form { Opacity = 0, ShowInTaskbar = false, ClientSize = new Size(430, 700) })
            {
                var first = (Control)Activator.CreateInstance(pickerType, Instance, null, new[] { store }, null);
                var second = (Control)Activator.CreateInstance(pickerType, Instance, null, new[] { store }, null);
                form.Controls.Add(first);
                form.Controls.Add(second);
                form.Show();
                Application.DoEvents();
                var voice = pickerType.GetProperty("SelectedVoice", Instance).GetValue(first);
                Check(first.Controls.Find("aiVoiceRemove", true).Single().Enabled, "Partial-only model can be removed");
                Check(first.Controls.Find("aiVoiceStorage", true).Single().Text.Contains("Yarım kalan"), "Partial storage is visible");
                Call(second, "SetAvailable", false);
                ((Task)Call(store, "RemoveAsync", voice, CancellationToken.None)).GetAwaiter().GetResult();
                Application.DoEvents();
                Check(!first.Controls.Find("aiVoiceRemove", true).Single().Enabled, "Removal automatically disables delete");
                Check(first.Controls.Find("aiVoiceDownload", true).Single().Enabled, "Removal automatically offers download again");
                Check(!second.Controls.Find("aiVoiceDownload", true).Single().Enabled, "Shared updates preserve other window's busy lock");
                Check(!first.Controls.Find("aiVoiceStorage", true).Single().Text.Contains("Yarım kalan")
                    && first.Controls.Find("aiVoiceStorage", true).Single().Text == second.Controls.Find("aiVoiceStorage", true).Single().Text,
                    "Background deletion refreshes both open storage views");
                foreach (Control action in first.Controls.Find("aiVoiceRemove", true).Single().Parent.Controls)
                    Check(action.Right <= action.Parent.ClientSize.Width, "Management buttons fit narrow picker");
            }
            var after = subscriptions.GetValue(null) as Delegate;
            Check((after == null ? 0 : after.GetInvocationList().Length) == initial, "Disposed windows release store subscriptions");
            Console.WriteLine("PASS: " + checks + " live model management checks; isolated partial file only.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
