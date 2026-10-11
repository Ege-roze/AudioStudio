using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using ses_uyg;

internal static class AiPrivacyTests
{
    private static int Main()
    {
        try { Run().GetAwaiter().GetResult(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static async Task Run()
    {
        var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var address = new Uri("http://127.0.0.1:" + port + "/");
        bool recordingDisabled = false;
        bool redirect = false;
        bool redirectFollowed = false;
        bool audioBeforeDisable = false;
        using (var listener = new HttpListener())
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            listener.Prefixes.Add(address.AbsoluteUri);
            listener.Start();
            var server = Task.Run(async () =>
            {
                try
                {
                    while (listener.IsListening)
                    {
                        var context = await listener.GetContextAsync();
                        string body;
                        using (var reader = new StreamReader(context.Request.InputStream)) body = await reader.ReadToEndAsync();
                        string path = context.Request.Url.AbsolutePath;
                        if (path == "/escaped") redirectFollowed = true;
                        string response = "{}";
                        if (redirect)
                        {
                            context.Response.StatusCode = 302;
                            context.Response.RedirectLocation = new Uri(address, "escaped").AbsoluteUri;
                            response = "redirects are not JSON";
                        }
                        else if (path == "/info") response = "{\"modelSlots\":[{\"modelFile\":\"tsukuyomi_v2_40k_e100_simple.onnx\",\"samplingRate\":40000,\"slotIndex\":0}]}";
                        else if (path == "/update_settings")
                        {
                            if (body == "key=recordIO&val=0") recordingDisabled = true;
                            response = "{\"pipelineInfo\":{}}";
                        }
                        else if (path == "/test")
                        {
                            if (!recordingDisabled) audioBeforeDisable = true;
                            var json = new JavaScriptSerializer();
                            var request = json.Deserialize<Dictionary<string,object>>(body);
                            response = json.Serialize(new { changedVoiceBase64 = request["buffer"] });
                        }
                        byte[] bytes = Encoding.UTF8.GetBytes(response);
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        context.Response.Close();
                    }
                }
                catch (Exception) when (!listener.IsListening) { }
            });
            try
            {
                var client = new AiVoiceClient(address, () => null);
                await client.PrepareFemaleVoice(12, timeout.Token);
                if (!recordingDisabled || audioBeforeDisable) throw new Exception("Audio recording was not disabled before audio.");
                redirect = true;
                try { await client.ConvertAudio(new byte[20], timeout.Token); throw new Exception("Redirect unexpectedly succeeded."); }
                catch (ArgumentException) { } // Non-JSON redirect response; never followed.
                catch (InvalidOperationException) { }
                if (redirectFollowed) throw new Exception("Local audio request followed a redirect.");
            }
            finally { listener.Stop(); await server; }
        }
        Console.WriteLine("PASS: Engine recording disabled before synthetic audio; local audio HTTP redirects are not followed.");
    }
}
