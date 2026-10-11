using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ses_uyg
{
    internal sealed class AiVoiceDefinition
    {
        internal readonly string Id, Name, RelativePath, Sha256, Credit, TermsPath;
        internal readonly long Bytes;
        internal readonly int Slot, SampleRate, BufferMilliseconds;
        internal readonly bool HasPitch;
        internal string FileName { get { return Path.GetFileName(RelativePath); } }
        internal bool IsOnnx { get { return FileName.EndsWith(".onnx", StringComparison.Ordinal); } }
        internal string Version { get { return IsOnnx && SampleRate == 16000 ? "v2.1" : "v2"; } }
        internal string ModelType { get { return IsOnnx ? (HasPitch ? "onnxRVC" : "onnxRVCNono") : (HasPitch ? "pyTorchRVCv2" : "pyTorchRVCv2Nono"); } }
        internal Uri DownloadUri { get { return new Uri(AiVoiceCatalog.Repository + "/resolve/" + AiVoiceCatalog.Revision + "/" + RelativePath); } }
        internal string TermsUrl { get { return AiVoiceCatalog.Repository + "/blob/" + AiVoiceCatalog.Revision + "/" + TermsPath; } }
        internal AiVoiceDefinition(string id, string name, int slot, string path, long bytes, string hash,
            int rate, bool pitch, int buffer, string credit, string terms)
        { Id = id; Name = name; Slot = slot; RelativePath = path; Bytes = bytes; Sha256 = hash; SampleRate = rate; HasPitch = pitch; BufferMilliseconds = buffer; Credit = credit; TermsPath = terms; }
        public override string ToString() { return Name; }
    }

    // The compatible female voices in VCClient 1.5.3.18a's official RVC sample catalog.
    // A single format per voice avoids downloading duplicate Torch/ONNX checkpoints.
    internal static class AiVoiceCatalog
    {
        internal const string Repository = "https://huggingface.co/wok000/vcclient_model";
        internal const string Revision = "c46962577db8cf3fba2b3fc3526af98eb6611840";
        internal static readonly IReadOnlyList<AiVoiceDefinition> All = Array.AsReadOnly(new[]
        {
            new AiVoiceDefinition("Tsukuyomi-chan_o", "Tsukuyomi-chan", 0, "rvc_v2_alpha/tsukuyomi-chan/tsukuyomi_v2_40k_e100_simple.onnx", 55382411,
                "99d294dd30b1fcb2881285af9e9fcf0f5fab19187703ffdd746d5fb7b725fcf6", 40000, true, 300, "Tsukuyomi-chan · CV. Rei Yumesaki", "rvc_v2_alpha/tsukuyomi-chan/terms_of_use.txt"),
            new AiVoiceDefinition("Amitaro_o", "Amitaro", 1, "rvc_v2_alpha/amitaro/amitaro_v2_40k_e100_simple.onnx", 55382411,
                "b7da32d31150d2191d5d2d6dd07dafd86b1cfdbe0a991871da4bb6c18cab0d39", 40000, true, 300, "Amitaro (あみたろ)", "rvc_v2_alpha/amitaro/terms_of_use.txt"),
            new AiVoiceDefinition("Amitaro_16k_o", "Amitaro · 16 kHz", 2, "rvc_v2_alpha/amitaro16k/amitaro_v2_16k.onnx", 66800599,
                "2d74e2fce8d5770e4b640ab45355286396b8308f5b09b059fb41a167592990c5", 16000, true, 300, "Amitaro (あみたろ)", "rvc_v2_alpha/amitaro16k/terms_of_use.txt"),
            new AiVoiceDefinition("Amitaro_16k_nof0_o", "Amitaro · 16 kHz No F0", 3, "rvc_v2_alpha/amitaro16k/amitaro_v2_16k_nof0.onnx", 66525277,
                "2c4df44fb3ac7bf7c70e0a6ab8b6f56125c65bb855b6b998c62a102bf48978b7", 16000, false, 300, "Amitaro (あみたろ)", "rvc_v2_alpha/amitaro16k/terms_of_use.txt"),
            Aiso("HOWATTO", "Howatto", 4, 55074309, "8fc32652b8ec472615af3f6661a50a195dd54990e0f517bd0dbd66db59813146"),
            Aiso("KAKKOII", "Kakkoii", 5, 55074309, "ae33b60655a59ecb37135892ed4a95ba326b412d6589dea251fced95e18aef31"),
            Aiso("SARASARA", "Sarasara", 6, 55074757, "ff8c3058dc6ef17f46bd178190cd38fdf46fc06f1d47c12fae55a0cc54398a4e"),
            Aiso("SITTORI", "Sittori", 7, 55074309, "7696d020ced92d9d5502ca40943ee7d35120988a92b70909fa099fc4e24a8477"),
            Aiso("SYAKITTO", "Syakitto", 8, 55074757, "569257d45848dada4ba604dfb9a60496d3043eb112e82c81d71eb37be942be50")
        });
        private static AiVoiceDefinition Aiso(string id, string name, int slot, long bytes, string hash)
        { return new AiVoiceDefinition("V2-AISO-" + id + "_t", "AISO · " + name, slot, "rvc_v2_chihaya_jinja/V2-AISO-" + id + ".pth", bytes, hash, 40000, false, 2040, "Tsuruno · Chihaya Jinja (鶴乃・ちはや神社)", "rvc_v2_chihaya_jinja/description.txt"); }
        internal static AiVoiceDefinition Find(string id) { return All.FirstOrDefault(v => v.Id == id) ?? All[0]; }
    }
}
