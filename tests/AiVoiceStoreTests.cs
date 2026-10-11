using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ses_uyg;

internal static class AiVoiceStoreTests
{
    private static int checks;
    private static int Main()
    {
        try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + checks + " voice catalog/store checks."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static async Task Run()
    {
        Check(AiVoiceCatalog.All.Count == 9 && AiVoiceCatalog.All.Select(v => v.Slot).Distinct().Count() == 9, "Distinct catalog slots");
        Check(AiVoiceCatalog.All.All(v => v.DownloadUri.Host == "huggingface.co" && v.DownloadUri.AbsolutePath.Contains(AiVoiceCatalog.Revision)), "Pinned same-publisher model URLs");
        Check(AiVoiceCatalog.All.Where(v => !v.HasPitch && !v.IsOnnx).All(v => v.BufferMilliseconds >= 2000), "AISO documented audio block length");
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voice-tests-" + Guid.NewGuid().ToString("N"));
        var store = new AiVoiceStore(root);
        byte[] bytes = Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray();
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        var a = new AiVoiceDefinition("test-a", "Test A", 0, "test-a.onnx", bytes.Length, hash, 40000, true, 300, "Test", "test.txt");
        var b = new AiVoiceDefinition("test-b", "Test B", 1, "test-b.onnx", bytes.Length, hash, 16000, false, 300, "Test", "test.txt");
        Check(!store.IsInstalled(a) && !Directory.Exists(root), "Opening catalog writes no files");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, ".voice-0.part"), bytes);
        await store.EnsureAsync(a, null, CancellationToken.None);
        Check(store.IsInstalled(a) && !store.IsInstalled(b), "Only requested voice is installed");
        Check(!File.Exists(Path.Combine(root, ".voice-0.part")), "Successful download cache is removed");
        await store.EnsureAsync(a, null, CancellationToken.None);
        Check(store.IsInstalled(a), "Installed verified model is reused without network");
        File.WriteAllBytes(Path.Combine(root, ".voice-1.part"), bytes);
        await store.EnsureAsync(b, null, CancellationToken.None);
        Check(store.IsInstalled(a) && store.IsInstalled(b), "Second model coexists without replacing first");
        string metadata = File.ReadAllText(Path.Combine(root, "1", "params.json"));
        Check(metadata.Contains("onnxRVCNono") && metadata.Contains("\"samplingRate\":16000") && metadata.Contains("\"defaultTune\":0"), "No F0 model metadata");
        Check(metadata.Contains("\"version\":\"v2.1\""), "16 kHz ONNX output schema version is preserved");
        File.WriteAllText(Path.Combine(root, "1", "params.json"), metadata.Replace("v2.1", "v2"));
        Check(!store.IsInstalled(b), "Incorrect metadata does not report a ready voice");
        await store.EnsureAsync(b, null, CancellationToken.None);
        Check(store.IsInstalled(b), "Verified existing model repairs metadata without downloading again");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await store.EnsureAsync(a, null, cancelled.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { checks++; }
        }
        var other = new AiVoiceDefinition("other", "Other", 0, "other.onnx", bytes.Length, hash, 40000, true, 300, "Test", "test.txt");
        try { await store.EnsureAsync(other, null, CancellationToken.None); throw new Exception("Occupied slot overwritten"); }
        catch (Exception ex) when (ex.Message == UiText.Get("AiVoiceSlotOccupied")) { checks++; }
        Check(store.IsInstalled(a), "Foreign slot conflict preserves installed model");
        File.Delete(store.ModelPath(b));
        File.WriteAllBytes(Path.Combine(root, ".voice-1.part"), new byte[bytes.Length]);
        try { await store.EnsureAsync(b, null, CancellationToken.None); throw new Exception("Corrupt download accepted"); }
        catch (Exception ex) when (ex.Message == UiText.Get("AiVoiceCorrupt")) { checks++; }
        Check(!store.IsInstalled(b) && !File.Exists(Path.Combine(root, ".voice-1.part")) && store.IsInstalled(a), "Corrupt selected download cannot affect other voices");
        File.WriteAllBytes(Path.Combine(root, ".voice-1.part"), bytes);
        await store.EnsureAsync(b, null, CancellationToken.None);
        var models = new[] { a, b };
        var usage = store.GetStorage(models);
        Check(usage.InstalledCount == 2 && usage.TotalBytes == bytes.Length * 2 && usage.PartialBytes == 0, "Storage counts actual model files, excluding metadata");
        File.WriteAllBytes(Path.Combine(root, ".voice-0.part"), new byte[123]);
        usage = store.GetStorage(models);
        Check(usage.TotalBytes == bytes.Length * 2 + 123 && usage.PartialBytes == 123, "Partial download space is included");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { await store.RemoveAsync(a, cancelled.Token); throw new Exception("Removal cancellation ignored"); }
            catch (OperationCanceledException) { checks++; }
        }
        Check(store.IsInstalled(a), "Cancelled removal preserves voice");
        using (store.AcquireRuntimeLease())
        using (store.AcquireRuntimeLease())
        {
            try { await store.RemoveAsync(a, CancellationToken.None); throw new Exception("Running model was removed"); }
            catch (Exception ex) when (ex.Message == UiText.Get("AiVoiceBusy")) { checks++; }
            try { await store.EnsureAsync(a, null, CancellationToken.None); throw new Exception("Running model was modified"); }
            catch (Exception ex) when (ex.Message == UiText.Get("AiVoiceBusy")) { checks++; }
        }
        Check(store.IsInstalled(a) && store.IsInstalled(b), "Runtime lease preserves all models");
        try { await store.RemoveAsync(other, CancellationToken.None); throw new Exception("Foreign slot metadata removed"); }
        catch (Exception ex) when (ex.Message == UiText.Get("AiVoiceSlotOccupied")) { checks++; }
        using (new FileStream(store.ModelPath(a), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            try { await store.RemoveAsync(a, CancellationToken.None); throw new Exception("Locked weight removed"); }
            catch (IOException) { checks++; }
        }
        Check(store.IsInstalled(a), "Failed removal keeps valid metadata for retry");
        string unrelated = Path.Combine(root, "0", "keep.txt"), engine = Path.Combine(root, "engine.exe");
        File.WriteAllText(unrelated, "Unrelated slot content");
        File.WriteAllText(engine, "Engine content");
        int notifications = 0;
        Action<string> onChanged = path => { if (path == store.DirectoryPath) notifications++; };
        AiVoiceStore.Changed += onChanged;
        try { await store.RemoveAsync(a, CancellationToken.None); }
        finally { AiVoiceStore.Changed -= onChanged; }
        Check(notifications == 1, "Removal notifies open views");
        Check(!store.IsInstalled(a) && !store.HasLocalFiles(a) && !File.Exists(Path.Combine(root, "0", "params.json")), "Selected weight, metadata and partial removed");
        Check(store.IsInstalled(b) && File.Exists(unrelated) && File.Exists(engine), "Other voices, unknown slot files and engine are preserved");
        usage = store.GetStorage(models);
        Check(usage.InstalledCount == 1 && usage.TotalBytes == bytes.Length, "Storage decreases after removal");
        File.WriteAllBytes(Path.Combine(root, ".voice-0.part"), bytes);
        await store.EnsureAsync(a, null, CancellationToken.None);
        Check(store.IsInstalled(a) && store.IsInstalled(b), "Deleted model can be downloaded again independently");
        await store.RemoveAsync(a, CancellationToken.None);
        File.WriteAllBytes(Path.Combine(root, ".voice-0.part"), new byte[123]);
        Check(store.HasLocalFiles(a), "Incomplete model can be selected for cleanup");
        await store.RemoveAsync(a, CancellationToken.None);
        Check(!store.HasLocalFiles(a) && store.IsInstalled(b), "Partial-only cleanup preserves other voices");

        string runtime = Path.Combine(root, "runtime", "MMVCServerSIO.exe");
        string legacy = Path.Combine(root, "runtime", "ses_models");
        string user = Path.Combine(root, "user-models");
        Directory.CreateDirectory(Path.Combine(legacy, "1"));
        Check(AiVoiceStore.FindStore(user, runtime).DirectoryPath == legacy, "Legacy store is found even after first model is removed");
        Directory.CreateDirectory(user);
        Check(AiVoiceStore.FindStore(user, runtime).DirectoryPath == user, "User model store still takes precedence");
        using (new AiVoiceStore(Path.Combine(root, "empty")).AcquireRuntimeLease()) { checks++; }
    }
}
