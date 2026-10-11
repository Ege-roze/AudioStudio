using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ses_uyg
{
    // VCClient 1.5 REST protocol. Only talks to our local, certificate-pinned server.
    internal sealed class AiVoiceClient
    {
        private readonly Uri address;
        private readonly Func<X509Certificate2> certificate;
        internal AiVoiceClient(Uri address, Func<X509Certificate2> certificate)
        {
            if (!address.IsLoopback) throw new ArgumentException("AI server must be local.");
            this.address = address;
            this.certificate = certificate;
        }

        internal Task<Dictionary<string, object>> Info(CancellationToken token)
        {
            return Request("info", null, null, token);
        }

        internal Task<Dictionary<string, object>> Setting(string key, object value, CancellationToken token)
        {
            string body = "key=" + Uri.EscapeDataString(key) + "&val=" + Uri.EscapeDataString(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            return Request("update_settings", body, "application/x-www-form-urlencoded", token);
        }

        internal Task PrepareFemaleVoice(int pitch, CancellationToken token)
        { return PrepareVoice(AiVoiceCatalog.All[0], pitch, token); }

        internal async Task PrepareVoice(AiVoiceDefinition voice, int pitch, CancellationToken token)
        {
            // The app has no recording feature. Explicitly override any engine-side
            // setting before model preparation or the first audio block.
            await Setting("recordIO", 0, token).ConfigureAwait(false);
            var info = await Info(token).ConfigureAwait(false);
            var female = FindVoice(info, voice);
            if (female == null) throw UiText.Error("AiModelMissing", voice.Name);
            await Setting("modelSlotIndex", female["slotIndex"], token).ConfigureAwait(false);
            await Setting("gpu", 0, token).ConfigureAwait(false);
            await Setting("passThrough", "false", token).ConfigureAwait(false);
            await Setting("enableServerAudio", 0, token).ConfigureAwait(false);
            await Setting("serverAudioStated", 0, token).ConfigureAwait(false);
            await Setting("inputSampleRate", 44100, token).ConfigureAwait(false);
            await Setting("outputSampleRate", 44100, token).ConfigureAwait(false);
            await Setting("f0Detector", "rmvpe_onnx", token).ConfigureAwait(false);
            await Setting("extraConvertSize", 16384, token).ConfigureAwait(false);
            await Setting("indexRatio", 0, token).ConfigureAwait(false);
            info = await Setting("tran", voice.HasPitch ? pitch : 0, token).ConfigureAwait(false);
            if (!info.ContainsKey("pipelineInfo") || info["pipelineInfo"] is string)
                throw UiText.Error("AiPipelineMissing");
            // Warm GPU kernels with synthetic audio, then flush it with silence.
            var warmup = new byte[44100 * voice.BufferMilliseconds / 1000 * 2];
            for (int i = 0; i < warmup.Length / 2; i++)
            {
                short sample = (short)(4000 * Math.Sin(2 * Math.PI * 150 * i / 44100));
                warmup[i * 2] = (byte)sample;
                warmup[i * 2 + 1] = (byte)(sample >> 8);
            }
            for (int i = 0; i < 3; i++) await ConvertAudio(warmup, token).ConfigureAwait(false);
            for (int i = 0; i < 3; i++) await ConvertAudio(new byte[warmup.Length], token).ConfigureAwait(false);
        }

        private static Dictionary<string, object> FindVoice(Dictionary<string, object> info, AiVoiceDefinition voice)
        {
            var slots = info.ContainsKey("modelSlots") ? info["modelSlots"] as object[] : null;
            return slots == null ? null : slots.OfType<Dictionary<string, object>>().FirstOrDefault(slot =>
                slot.ContainsKey("modelFile") && slot.ContainsKey("samplingRate") && Convert.ToInt32(slot["samplingRate"]) > 0 &&
                slot.ContainsKey("slotIndex") && Convert.ToInt32(slot["slotIndex"]) == voice.Slot &&
                string.Equals(Path.GetFileName(Convert.ToString(slot["modelFile"])), voice.FileName, StringComparison.OrdinalIgnoreCase));
        }

        internal async Task<byte[]> ConvertAudio(byte[] pcm, CancellationToken token)
        {
            var json = new JavaScriptSerializer();
            string body = json.Serialize(new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), buffer = Convert.ToBase64String(pcm) });
            var response = await Request("test", body, "application/json", token).ConfigureAwait(false);
            object encoded;
            if (!response.TryGetValue("changedVoiceBase64", out encoded) || !(encoded is string)) throw UiText.Error("AiBadResponse");
            byte[] result = Convert.FromBase64String((string)encoded);
            // VCClient reports inference failures as a 1/1024-sample silent result.
            if (result.Length != pcm.Length) throw UiText.Error("AiBadResponse");
            return result;
        }

        private async Task<Dictionary<string, object>> Request(string path, string body, string contentType, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = (HttpWebRequest)WebRequest.Create(new Uri(address, path));
            request.Proxy = null;
            request.AllowAutoRedirect = false; // Local audio must never follow a redirect.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            request.ServicePoint.Expect100Continue = false;
            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;
            if (address.Scheme == "https")
                request.ServerCertificateValidationCallback = (sender, cert, chain, errors) =>
                {
                    using (var expected = certificate())
                        return cert != null && expected != null && expected.GetRawCertData().SequenceEqual(cert.GetRawCertData());
                };
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(120));
                using (timeout.Token.Register(request.Abort))
                {
                    try
                    {
                        if (body != null)
                        {
                            request.Method = "POST";
                            request.ContentType = contentType;
                            byte[] data = Encoding.UTF8.GetBytes(body);
                            request.ContentLength = data.Length;
                            using (var stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                                await stream.WriteAsync(data, 0, data.Length, timeout.Token).ConfigureAwait(false);
                        }
                        using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                        using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            var json = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 };
                            var result = json.DeserializeObject(await reader.ReadToEndAsync().ConfigureAwait(false)) as Dictionary<string, object>;
                            if (result == null) throw UiText.Error("AiBadResponse");
                            return result;
                        }
                    }
                    catch (WebException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
                }
            }
        }
    }

    // The capture callback never waits for inference. At most two blocks are queued.
    internal sealed class AiVoiceSession : IDisposable
    {
        private readonly object sync = new object();
        private readonly Queue<byte[]> queue = new Queue<byte[]>();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Func<byte[], CancellationToken, Task<byte[]>> convert;
        private readonly Action<byte[], double> output;
        private readonly Action<Exception> failed;
        private bool disposed;
        internal Task Completion { get; private set; }

        internal AiVoiceSession(Func<byte[], CancellationToken, Task<byte[]>> convert, Action<byte[], double> output, Action<Exception> failed)
        {
            this.convert = convert;
            this.output = output;
            this.failed = failed;
            Completion = Task.Run(Run);
        }

        internal void Enqueue(byte[] data, int length)
        {
            lock (sync)
            {
                if (disposed) return;
                if (queue.Count >= 2) throw UiText.Error("AiTooSlow");
                var copy = new byte[length];
                Buffer.BlockCopy(data, 0, copy, 0, length);
                queue.Enqueue(copy);
                signal.Release();
            }
        }

        private async Task Run()
        {
            try
            {
                while (true)
                {
                    await signal.WaitAsync(stop.Token).ConfigureAwait(false);
                    byte[] block;
                    lock (sync) { if (disposed) return; block = queue.Dequeue(); }
                    var watch = Stopwatch.StartNew();
                    var converted = await convert(block, stop.Token).ConfigureAwait(false);
                    stop.Token.ThrowIfCancellationRequested();
                    output(converted, watch.Elapsed.TotalMilliseconds);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!stop.IsCancellationRequested) failed(ex); }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                queue.Clear();
            }
            stop.Cancel();
            Completion.ContinueWith(_ => { signal.Dispose(); stop.Dispose(); }, TaskScheduler.Default);
        }
    }
}
