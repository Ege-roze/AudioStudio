using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class LegalDocumentTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Range { internal int Min, Max; }
    [StructLayout(LayoutKind.Sequential)] private struct FormatRange
    { internal IntPtr Hdc, Target; internal Rect Bounds, Page; internal Range Characters; }
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    // WinForms DrawToBitmap does not draw RichTextBox content. Render the control's
    // own RichEdit text into the preview with its native formatting API instead.
    private static void RenderDocument(RichTextBox body, Form form, Bitmap bitmap)
    {
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var point = body.PointToScreen(Point.Empty);
            point.Offset(-form.Left, -form.Top);
            graphics.FillRectangle(SystemBrushes.Window, new Rectangle(point, body.Size));
            float x = 1440f / graphics.DpiX, y = 1440f / graphics.DpiY;
            var bounds = new Rect { Left = (int)(point.X * x), Top = (int)(point.Y * y),
                Right = (int)((point.X + body.Width - 18) * x), Bottom = (int)((point.Y + body.Height) * y) };
            IntPtr dc = graphics.GetHdc();
            IntPtr memory = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(FormatRange)));
            try
            {
                Marshal.StructureToPtr(new FormatRange { Hdc = dc, Target = dc, Bounds = bounds,
                    Page = bounds, Characters = new Range { Min = 0, Max = -1 } }, memory, false);
                SendMessage(body.Handle, 1081, new IntPtr(1), memory);
                SendMessage(body.Handle, 1081, IntPtr.Zero, IntPtr.Zero);
            }
            finally { Marshal.FreeCoTaskMem(memory); graphics.ReleaseHdc(dc); }
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var type = assembly.GetType("ses_uyg.LegalDocumentForm", true);
            var documents = new[] { "privacy-policy", "terms-of-service", "third-party-notices", "model-terms" };
            foreach (string language in new[] { "tr", "en" })
            foreach (string document in documents)
            {
                string file = document + "." + language + ".txt";
                using (var stream = assembly.GetManifestResourceStream("ses_uyg.Legal." + file))
                {
                    if (stream == null) throw new Exception("Missing offline resource: " + file);
                    using (var reader = new StreamReader(stream))
                        if (reader.ReadToEnd() != File.ReadAllText(Path.Combine(args[1], file)))
                            throw new Exception("Offline/site policy mismatch: " + file);
                }
            }
            using (var form = (Form)Activator.CreateInstance(type, Instance, null, new object[] { 1 }, null))
            {
                var tabs = (TabControl)type.GetField("tabs", Instance).GetValue(form);
                var language = (ComboBox)type.GetField("language", Instance).GetValue(form);
                if (tabs.SelectedIndex != 1 || tabs.TabCount != 4) throw new Exception("Wrong document opened.");
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.Show();
                Directory.CreateDirectory(args[2]);
                for (int lang = 0; lang <= 1; lang++)
                {
                    language.SelectedIndex = lang;
                    for (int i = 0; i < tabs.TabCount; i++)
                    {
                        tabs.SelectedIndex = i;
                        Application.DoEvents();
                        var body = (RichTextBox)tabs.TabPages[i].Controls[0];
                        if (!body.ReadOnly || body.Text.Length < 300 || body.Width < 350 || body.Height < 200)
                            throw new Exception("Unreadable document: " + i);
                        if (body.Text.Contains("\uFFFD") || body.Text.Contains("{{"))
                            throw new Exception("Encoding/template error in " + i);
                        bool draft = File.ReadAllText(Path.Combine(args[1], "privacy-policy.en.txt")).Contains("PRE-RELEASE DRAFT");
                        bool showsDraft = body.Text.Contains("YAYIN ÖNCESİ TASLAK") || body.Text.Contains("PRE-RELEASE DRAFT");
                        if (i < 3 && showsDraft != draft)
                            throw new Exception("Document publication status differs from generated policies.");
                        if (!draft && (body.Text.Contains("[PUBLISHER_NAME") || body.Text.Contains("[CONTACT_EMAIL")))
                            throw new Exception("Published document contains placeholder contact details.");
                    }
                    tabs.SelectedIndex = 0;
                    form.ClientSize = new Size(lang == 0 ? 900 : 550, lang == 0 ? 680 : 430);
                    Application.DoEvents();
                    using (var bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        RenderDocument((RichTextBox)tabs.TabPages[0].Controls[0], form, bitmap);
                        bitmap.Save(Path.Combine(args[2], "legal-" + (lang == 0 ? "tr" : "en-small") + ".png"));
                    }
                }
                form.Close();
            }
            var mainType = assembly.GetType("ses_uyg.Form1", true);
            using (var main = (Form)Activator.CreateInstance(mainType, Instance, null,
                new object[] { Path.Combine(Path.GetTempPath(), "ses-legal-test-" + Guid.NewGuid().ToString("N")) }, null))
            {
                if (main.Controls.Find("PrivacyPolicy", true).Length == 0 || main.Controls.Find("TermsOfService", true).Length == 0)
                    throw new Exception("Policies unavailable before microphone start.");
                if (mainType.GetField("waveIn", Instance).GetValue(main) != null)
                    throw new Exception("Opening the app started capture.");
                using (var settings = (Form)Activator.CreateInstance(assembly.GetType("ses_uyg.SettingsForm"), Instance, null, new object[] { main }, null))
                    if (settings.Controls.Find("ThirdPartyNotices", true).Length == 0) throw new Exception("Missing Settings link.");
                using (var about = (Form)Activator.CreateInstance(assembly.GetType("ses_uyg.AboutForm"), true))
                    if (about.Controls.Find("TermsOfService", true).Length == 0) throw new Exception("Missing About link.");
            }
            Console.WriteLine("PASS: 8 embedded/exported policies match, bilingual read-only reader, small-window layout, publication status, links before capture and in Settings/About. No microphone opened.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
