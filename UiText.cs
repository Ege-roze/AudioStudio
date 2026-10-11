using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ses_uyg
{
    internal enum UiLanguage { Turkish, English }

    // Keep the message key and its values, so changing language also updates
    // existing status messages without restarting audio or rebuilding controls.
    internal static class UiText
    {
        private sealed class Message
        {
            internal string Key;
            internal object[] Arguments;
        }

        private sealed class LocalizedException : InvalidOperationException
        {
            private readonly string key;
            private readonly object[] arguments;
            internal LocalizedException(string key, object[] arguments)
            {
                this.key = key;
                this.arguments = arguments;
            }
            public override string Message { get { return Get(key, arguments); } }
        }

        private static readonly ConditionalWeakTable<Control, Message> bindings =
            new ConditionalWeakTable<Control, Message>();

        internal static UiLanguage Language { get; set; } = UiLanguage.Turkish;

        // Each entry contains Turkish followed by English. Device and file names
        // are supplied as arguments and are never translated.
        private static readonly Dictionary<string, string[]> messages = new Dictionary<string, string[]>
        {
            { "SettingsTitle", new[] { "Ayarlar", "Settings" } },
            { "SettingsDescription", new[] { "Ses cihazlarını ve uygulamanın açılış / kapanış davranışını seç.", "Choose your audio devices and how the app opens and closes." } },
            { "SettingsAudio", new[] { "Ses cihazları", "Audio devices" } },
            { "SettingsBehavior", new[] { "Açılış ve kapanış", "Opening and closing" } },
            { "SettingsCloseToTray", new[] { "Çarpıya basınca arka planda çalışmaya devam et", "Keep running in the background when I click X" } },
            { "SettingsCloseHint", new[] { "Kapalıysa çarpı uygulamadan tamamen çıkar. Uygulamayı kapat düğmesi her zaman tam çıkış yapar.", "When off, X exits the app completely. Exit application always performs a full exit." } },
            { "SettingsStartInTray", new[] { "Uygulamayı sistem tepsisinde aç", "Open the app in the system tray" } },
            { "SettingsStartWithWindows", new[] { "Windows açılınca uygulamayı otomatik başlat", "Start the app automatically when Windows starts" } },
            { "SettingsStartupHint", new[] { "Açılış tercihleri sonraki başlatmada geçerlidir. Ses aktarımı kendiliğinden başlamaz. Gizli pencereyi saat yanındaki simgeye çift tıklayarak açabilirsin.", "Opening preferences apply on the next launch. Audio does not start automatically. Double-click the icon near the clock to show a hidden window." } },
            { "SettingsDeviceHint", new[] { "Bu seçimler ana aktarım için kullanılır. Test penceresinin dinleme cihazı ayrıca seçilir.", "These devices are used for the main stream. Choose the listening device separately in the test window." } },
            { "SettingsDevicesLocked", new[] { "Cihaz değiştirmek için önce aktarımı / AI testini durdur ve devam eden kurulumu bitir. Açılış ve kapanış tercihlerini değiştirebilirsin.", "Stop streaming / AI testing and finish any installation before changing devices. You can still change opening and closing preferences." } },
            { "SettingsDevicesMissing", new[] { "Mikrofon veya sanal çıkış bulunamadı. Cihazını bağlayıp listeyi yenile; VB-CABLE kurulumu ana ekranda. Diğer ayarları kaydedebilirsin.", "A microphone or virtual output is missing. Connect your device and refresh; VB-CABLE setup is on the main page. You can still save other settings." } },
            { "SettingsSaveHint", new[] { "Değişiklikler Kaydet'e bastığında uygulanır.", "Changes are applied when you click Save." } },
            { "SettingsSaveError", new[] { "Ayarlar kaydedilemedi: {0}", "Could not save settings: {0}" } },
            { "SettingsStartupError", new[] { "Windows başlangıç ayarı okunamadı; diğer tercihleri düzenleyebilirsin: {0}", "Could not read the Windows startup setting; other preferences can still be changed: {0}" } },
            { "ExitApplication", new[] { "Uygulamayı kapat", "Exit application" } },
            { "ShowApplication", new[] { "Uygulamayı göster", "Show application" } },
            { "Personalize", new[] { "Kişiselleştir", "Personalize" } },
            { "AppearanceTitle", new[] { "Görünümünü kişiselleştir", "Personalize your appearance" } },
            { "AppearanceDescription", new[] { "Görünüm paketini seç ve önizle. Kaydettiğin tasarım uygulamanın tüm pencerelerinde kullanılır.", "Choose and preview an appearance pack. Your saved design is used across all app windows." } },
            { "AppearanceTheme", new[] { "Görünüm paketi", "Appearance pack" } },
            { "AppearanceThemeGlass", new[] { "Cam · Mevcut tasarım", "Glass · Original design" } },
            { "AppearanceThemeClassic", new[] { "Klasik · iTunes / iPod", "Classic · iTunes / iPod" } },
            { "AppearanceThemeGlassHint", new[] { "Camsı paneller ve yumuşak ışık yansımaları. Arka planını ve cam efektini aşağıdan ayarlayabilirsin.", "Glass panels and soft reflections. Customize your background and glass effect below." } },
            { "AppearanceThemeClassicHint", new[] { "Eski iTunes ve iPod'dan esinlenen gümüş paneller, mavi düğmeler ve LCD görünümü.", "Silver panels, blue buttons and an LCD look inspired by classic iTunes and iPod." } },
            { "AppearanceClassicControlsHint", new[] { "Klasik paket metal arka plan kullanır. Renk, görsel ve cam ayarların Cam paketine döndüğünde korunur.", "The Classic pack uses a metal background. Your colors, image and glass settings are kept for when you return to Glass." } },
            { "AppearanceMode", new[] { "Arka plan türü", "Background type" } },
            { "AppearanceDefault", new[] { "Varsayılan görünüm", "Default appearance" } },
            { "AppearanceSolid", new[] { "Tek renk", "Solid color" } },
            { "AppearanceGradient", new[] { "Renk geçişi", "Gradient" } },
            { "AppearanceImage", new[] { "Görsel", "Image" } },
            { "AppearancePresets", new[] { "Hazır renkler", "Color presets" } },
            { "AppearanceLavender", new[] { "Lavanta", "Lavender" } },
            { "AppearanceOcean", new[] { "Okyanus", "Ocean" } },
            { "AppearanceSunset", new[] { "Gün batımı", "Sunset" } },
            { "AppearanceFirstColor", new[] { "Birinci renk", "First color" } },
            { "AppearanceSecondColor", new[] { "İkinci renk", "Second color" } },
            { "AppearanceAngle", new[] { "Geçiş açısı (°)", "Gradient angle (°)" } },
            { "AppearanceChooseImage", new[] { "Bilgisayardan görsel seç", "Choose an image" } },
            { "AppearanceImageFilter", new[] { "Görseller (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" } },
            { "AppearanceNoImage", new[] { "Henüz görsel seçilmedi.", "No image selected." } },
            { "AppearanceSavedImage", new[] { "Kaydedilen arka plan görseli", "Saved background image" } },
            { "AppearancePlacement", new[] { "Görselin yerleşimi", "Image placement" } },
            { "AppearanceFill", new[] { "Doldur · Gerekirse kenarları kırp", "Fill · Crop edges when needed" } },
            { "AppearanceFit", new[] { "Sığdır · Görselin tamamını göster", "Fit · Show the whole image" } },
            { "AppearanceStretch", new[] { "Esnet · Pencereye göre genişlet", "Stretch · Match the window" } },
            { "AppearanceLightness", new[] { "Arka planı açıklaştır", "Lighten the background" } },
            { "AppearancePanelTransparency", new[] { "Panel cam efekti", "Panel glass effect" } },
            { "AppearancePanelTransparencyValue", new[] { "Panel cam efekti: %{0}", "Panel glass effect: {0}%" } },
            { "AppearancePanelTransparencyHint", new[] { "%0: opak · %100: belirgin cam görünümü. Bulanıklık, parlak kenarlar ve ışık yansımaları korunur. Tüm pencerelerdeki cam panellere uygulanır.", "0%: opaque · 100%: pronounced glass effect. Blur, bright edges and reflections stay visible. Applies to glass panels in every window." } },
            { "AppearanceLightnessValue", new[] { "Arka planı açıklaştır: %{0}", "Lighten the background: {0}%" } },
            { "AppearanceLightnessHint", new[] { "Koyu görsellerde bu değeri artırarak yazıları daha rahat okuyabilirsin.", "Increase this for easier reading over dark images or colors." } },
            { "AppearancePreview", new[] { "Önizleme", "Preview" } },
            { "AppearancePreviewHint", new[] { "Değişiklikler Kaydet'e basana kadar yalnızca burada görünür. Seçtiğin görselin bir kopyası saklanır; asıl dosya değişmez.", "Changes appear only here until you click Save. A copy of your image is kept; the original file stays unchanged." } },
            { "AppearanceSample", new[] { "Ses panelleri seçtiğin arka planın üzerinde böyle görünür.", "This is how the audio panels look over your background." } },
            { "AppearanceReset", new[] { "Varsayılana dön", "Restore default" } },
            { "AppearanceCancel", new[] { "Vazgeç", "Cancel" } },
            { "AppearanceSave", new[] { "Kaydet", "Save" } },
            { "AppearanceImageRequired", new[] { "Görsel arka plan için önce bilgisayarından bir görsel seç.", "Choose an image before saving an image background." } },
            { "AppearanceImageLarge", new[] { "Bu görsel çok büyük. En fazla 40 milyon piksellik bir görsel seç.", "This image is too large. Choose an image with at most 40 million pixels." } },
            { "AppearanceImageError", new[] { "Görsel açılamadı: {0}", "Could not open the image: {0}" } },
            { "AppearanceSaveError", new[] { "Görünüm kaydedilemedi: {0}", "Could not save the appearance: {0}" } },
            { "MoreInfo", new[] { "Daha fazla bilgi", "More information" } },
            { "LegalDocuments", new[] { "Gizlilik, Koşullar ve Lisanslar", "Privacy, Terms and Licenses" } },
            { "PrivacyPolicy", new[] { "Gizlilik Politikası", "Privacy Policy" } },
            { "TermsOfService", new[] { "Kullanım Koşulları", "Terms of Service" } },
            { "ThirdPartyNotices", new[] { "Üçüncü Taraf Bildirimleri", "Third-Party Notices" } },
            { "ModelTerms", new[] { "Ses Modeli Koşulları", "Voice Model Terms" } },
            { "PrivacyShort", new[] { "Ses bilgisayarınızda işlenir. Yerel dosyalar, AI indirmeleri ve kontrol seçenekleri aşağıda açıklanır.", "Audio is processed on your computer. Local files, AI downloads and your controls are explained below." } },
            { "AiNetworkDisclosure", new[] { "AI'ı başlatmak indirme sunucularına bağlantı kurabilir; bu sunucular IP adresinizi görür. Ses dönüştürme yereldir. Mevcut motor ağ bağlantılarını da kabul edebilir; ayrıntılar Gizlilik Politikası'ndadır.", "Starting AI may contact download servers, which see your IP address. Voice conversion is local. The current engine may also accept network connections; see the Privacy Policy." } },
            { "LegalTitle", new[] { "Kaynaklar ve Haklar", "Sources and Rights" } },
            { "LegalIntro", new[] { "Ses Stüdyosu, NAudio ve isteğe bağlı VCClient/RVC bileşenlerinden yararlanır. Motor, modeller ve sürücüler farklı lisanslara tabidir.", "Audio Studio uses NAudio and optional VCClient/RVC components. The engine, models and drivers have separate licenses." } },
            { "LegalModel", new[] { "Tsukuyomi-chan ses modeli, Rei Yumesaki'nin ses verilerini kullanır ve kendine özgü kullanım koşullarına tabidir.", "The Tsukuyomi-chan voice model uses voice data by Rei Yumesaki and is subject to its own terms of use." } },
            { "LegalCable", new[] { "VB-CABLE, VB-Audio'nun bağış esaslı lisanslanan ürünüdür; bağış ve lisans desteği teşvik edilir.", "VB-CABLE is a donationware product by VB-Audio; donations and license support are encouraged." } },
            { "LegalRights", new[] { "Üçüncü taraf bileşenlerin telif, marka ve varsa patent hakları ilgili hak sahiplerine aittir. Bu bilgilendirme, bileşenlerin tam lisans ve kullanım koşullarının yerine geçmez.", "Copyright, trademark and any patent rights in third-party components belong to their respective rights holders. This notice does not replace the components' full licenses and terms of use." } },
            { "LegalLinks", new[] { "Lisanslar ve kullanım koşulları", "Licenses and terms of use" } },
            { "LegalVcClient", new[] { "VCClient / RVC · MIT", "VCClient / RVC · MIT" } },
            { "LegalNAudio", new[] { "NAudio · MIT", "NAudio · MIT" } },
            { "LegalModelLink", new[] { "Tsukuyomi-chan · Kullanım koşulları", "Tsukuyomi-chan · Terms of use" } },
            { "LegalCableLink", new[] { "VB-CABLE · Bağış ve lisans", "VB-CABLE · Donations and licensing" } },
            { "LegalClose", new[] { "Kapat", "Close" } },
            { "AiTitle", new[] { "AI Ses Değiştirici", "AI Voice Changer" } },
            { "AiDescription", new[] { "Konuşmanı kadın sesine dönüştür. Ses bilgisayarında işlenir ve seçtiğin sanal mikrofona aktarılır.", "Transform your speech into a female voice. Audio is processed on your computer and sent to your virtual microphone." } },
            { "AiFemale", new[] { "Kadın sesleri", "Female voices" } },
            { "AiVoiceSelect", new[] { "Ses modeli seç", "Choose a voice model" } },
            { "AiVoiceDownload", new[] { "Seçili sesi indir", "Download selected voice" } },
            { "AiVoiceDownloaded", new[] { "Ses indirildi", "Voice downloaded" } },
            { "AiVoiceInstalled", new[] { "{0} · İndirildi · {1:N1} MB", "{0} · Downloaded · {1:N1} MB" } },
            { "AiVoiceNotInstalled", new[] { "{0} · İndirilmedi · {1:N1} MB", "{0} · Not downloaded · {1:N1} MB" } },
            { "AiVoiceSharedEngine", new[] { "Yalnızca seçtiğin ses indirilir. Tüm sesler aynı AI motorunu kullanır.", "Only the voice you select is downloaded. All voices share one AI engine." } },
            { "AiVoiceNoPitch", new[] { "Bu sürümde ton ayarı kullanılmaz. Diğer seslerden bağımsız indirilir.", "Pitch adjustment does not apply to this version. It downloads independently." } },
            { "AiVoiceLongBuffer", new[] { "Bu ses yaklaşık 2 saniyelik ses parçalarıyla çalışır; konuşma gecikmesi daha yüksektir. Ton ayarı kullanılmaz.", "This voice uses roughly 2-second audio blocks, so speaking latency is higher. Pitch adjustment does not apply." } },
            { "AiVoiceCredit", new[] { "{0} · Kaynak ve kullanım koşulları", "{0} · Source and terms of use" } },
            { "AiVoiceDownloading", new[] { "{0} indiriliyor: %{1} · {2:N1} / {3:N1} MB", "Downloading {0}: {1}% · {2:N1} / {3:N1} MB" } },
            { "AiVoiceVerifying", new[] { "{0} doğrulanıyor: %{1}", "Verifying {0}: {1}%" } },
            { "AiVoiceDownloadComplete", new[] { "{0} indirildi. Artık bu sesi etkinleştirebilir veya test edebilirsin.", "{0} downloaded. You can now enable or test this voice." } },
            { "AiVoiceManagement", new[] { "Model yönetimi", "Model management" } },
            { "AiVoiceStorage", new[] { "{0}/{1} ses indirildi · Toplam: {2:N1} MB\nOrtak AI motoru bu toplama dahil değildir.", "{0}/{1} voices downloaded · Total: {2:N1} MB\nThe shared AI engine is not included." } },
            { "AiVoiceStoragePartial", new[] { "{0}/{1} ses indirildi · Toplam: {2:N1} MB\nYarım kalan indirmeler: {3:N1} MB (toplama dahil).\nOrtak AI motoru bu toplama dahil değildir.", "{0}/{1} voices downloaded · Total: {2:N1} MB\nPartial downloads: {3:N1} MB (included in total).\nThe shared AI engine is not included." } },
            { "AiVoiceStorageUnavailable", new[] { "Modellerin disk alanı bilgisi okunamadı.", "Model disk usage could not be read." } },
            { "AiVoiceRemove", new[] { "Seçili modeli sil", "Delete selected model" } },
            { "AiVoiceRemoveConfirm", new[] { "{0} modelinin dosyaları ve varsa yarım kalan indirmesi silinsin mi?\n\nBu sesi daha sonra yeniden indirebilirsin. Diğer sesler ve ortak AI motoru korunur.", "Delete the files and any partial download for {0}?\n\nYou can download this voice again later. Other voices and the shared AI engine will be kept." } },
            { "AiVoiceRemoving", new[] { "{0} siliniyor…", "Deleting {0}…" } },
            { "AiVoiceRemoved", new[] { "{0} silindi. İstediğin zaman yeniden indirebilirsin.", "{0} deleted. You can download it again at any time." } },
            { "AiVoiceBusy", new[] { "AI motoru modelleri kullanıyor veya başka bir model işlemi sürüyor. AI aktarımını/testini durdurup işlemin bitmesini bekle ve tekrar dene.", "The AI engine is using the models, or another model operation is in progress. Stop AI streaming/testing, wait for the operation to finish, and retry." } },
            { "AiVoiceSlotOccupied", new[] { "Bu sesin model yuvasında farklı bir model var. Mevcut dosyalar korunarak işlem durduruldu.", "This voice's slot contains a different model. The operation stopped to preserve existing files." } },
            { "AiVoiceSpace", new[] { "Bu ses için diskte yaklaşık {0} MB boş alan gerekiyor.", "This voice needs about {0} MB of free disk space." } },
            { "AiVoiceCorrupt", new[] { "Ses dosyası doğrulanamadı. Yeniden indirmeyi dene.", "The voice file failed verification. Try downloading it again." } },
            { "AiPitch", new[] { "Ton (yarım ses)", "Pitch (semitones)" } },
            { "AiEnable", new[] { "AI sesi etkinleştir", "Enable AI voice" } },
            { "AiDisable", new[] { "AI kapat / İptal", "Disable AI / Cancel" } },
            { "AiNotInstalled", new[] { "AI motoru kurulu değil. AI sesi etkinleştir düğmesine bastığında otomatik indirilip kurulacak.", "The AI engine is not installed. Click Enable AI voice to download and install it automatically." } },
            { "AiInstallHint", new[] { "İlk kurulum: 3,3 GB indirme ve yaklaşık 12 GB boş alan; modeller ayrıca indirilir. 64 bit Windows ve NVIDIA/CUDA için hazırlanmıştır.", "First setup: 3.3 GB download and about 12 GB free space; models download separately. Built for 64-bit Windows and NVIDIA/CUDA." } },
            { "AiDownloading", new[] { "AI motoru indiriliyor: %{0} · {1:N0} / {2:N0} MB. İptal edersen daha sonra kaldığın yerden devam edebilirsin.", "Downloading AI engine: {0}% · {1:N0} / {2:N0} MB. You can cancel and resume later." } },
            { "AiVerifying", new[] { "İndirilen AI motoru doğrulanıyor: %{0}", "Verifying the downloaded AI engine: {0}%" } },
            { "AiExtracting", new[] { "AI motoru kuruluyor: %{0} · {1:N0} / {2:N0} MB", "Installing AI engine: {0}% · {1:N0} / {2:N0} MB" } },
            { "AiCancelling", new[] { "AI hazırlığı iptal ediliyor…", "Cancelling AI preparation…" } },
            { "AiCancelled", new[] { "AI hazırlığı iptal edildi. Yeniden etkinleştirdiğinde indirme kaldığı yerden devam eder.", "AI preparation cancelled. Enable again to resume any unfinished download." } },
            { "AiInstallBusy", new[] { "AI motoru başka bir uygulama penceresinde kuruluyor. Kurulumun bitmesini bekleyip yeniden deneyin.", "Another application window is installing the AI engine. Wait for it to finish and try again." } },
            { "AiDownloadInvalid", new[] { "İndirme sunucusu beklenen dosyayı döndürmedi. İnternet bağlantısını kontrol edip yeniden etkinleştirin.", "The download server returned an unexpected file. Check your connection and enable AI again." } },
            { "AiDownloadIncomplete", new[] { "AI indirmesi tamamlanamadı. Yeniden etkinleştirerek kaldığı yerden devam edin.", "The AI download was interrupted. Enable AI again to resume." } },
            { "AiDownloadTimeout", new[] { "İndirme bağlantısı zaman aşımına uğradı. İnternet bağlantısını kontrol edip yeniden etkinleştirin; indirme devam edecek.", "The download connection timed out. Check your connection and enable AI again to resume." } },
            { "AiPackageCorrupt", new[] { "İndirilen AI dosyası doğrulanamadı. Yeniden etkinleştirdiğinizde temiz bir kopya indirilecek.", "The AI download failed verification. Enable AI again to download a fresh copy." } },
            { "AiArchivePath", new[] { "AI kurulum dosyası veya hedef klasörü güvenli biçimde açılamadı.", "The AI installation file or destination folder could not be opened safely." } },
            { "AiArchiveInvalid", new[] { "AI paketi gerekli motor dosyalarını içermiyor. Kurulum tamamlanmadı.", "The AI package is missing required engine files. Installation was not completed." } },
            { "AiSpaceNeeded", new[] { "AI kurulumu için hedef diskte en az {0} GB boş alan gerekiyor. Yer açıp yeniden deneyin.", "AI installation needs at least {0} GB free on the destination drive. Free some space and try again." } },
            { "AiWindows64Required", new[] { "Bu AI motoru 64 bit Windows gerektiriyor.", "This AI engine requires 64-bit Windows." } },
            { "AiIdle", new[] { "AI kapalı · AI sesi etkinleştir düğmesine basarak başla. Başlangıç tonu: +12 yarım ses.", "AI is off · Click Enable AI voice to begin. Starting pitch: +12 semitones." } },
            { "AiPreparing", new[] { "AI motoru açılıyor… İlk açılışta model dosyaları indirilebilir; birkaç dakika sürebilir.", "Starting the AI engine… The first launch may download models and take a few minutes." } },
            { "AiLoading", new[] { "Kadın sesi GPU'ya yükleniyor ve hazırlanıyor…", "Loading and warming up the female voice on the GPU…" } },
            { "AiReady", new[] { "Kadın sesi hazır · Mikrofonunu seçip Aktarımı başlat'a bas.", "Female voice ready · Select your microphone and click Start streaming." } },
            { "AiStreaming", new[] { "AI açık · Kadın sesi sanal mikrofona aktarılıyor.", "AI is on · The female voice is streaming to your virtual microphone." } },
            { "AiTimingHint", new[] { "AI ek gecikme oluşturur. Test et ile sonucu kulaklığından dinleyebilirsin.", "AI adds latency. Use Test audio to hear the result through headphones." } },
            { "AiTiming", new[] { "AI işlem süresi: {0} ms / {1} ms ses · Toplam gecikmeye cihaz tamponları da eklenir.", "AI processing: {0} ms / {1} ms audio · Device buffers add to total latency." } },
            { "AiCredit", new[] { "Model: Tsukuyomi-chan (CV. Rei Yumesaki) · VCClient / RVC · Kullanım koşulları", "Model: Tsukuyomi-chan (CV. Rei Yumesaki) · VCClient / RVC · Terms of use" } },
            { "AiError", new[] { "AI başlatılamadı: {0}", "Could not start AI: {0}" } },
            { "AiStreamError", new[] { "AI aktarımı durdu: {0}", "AI streaming stopped: {0}" } },
            { "AiRuntimeMissing", new[] { "AI motoru bulunamadı. AI sesi etkinleştir düğmesine yeniden basarak otomatik kurulumu başlatın.", "AI engine not found. Click Enable AI voice again to start automatic installation." } },
            { "AiModelMissing", new[] { "{0} modeli bulunamadı. Seçili sesi indirip yeniden deneyin.", "{0} model not found. Download the selected voice and try again." } },
            { "AiPipelineMissing", new[] { "Ses modeli GPU üzerinde hazırlanamadı. AI motoru günlüğünü kontrol edin.", "The voice model could not initialize on the GPU. Check the AI engine log." } },
            { "AiBadResponse", new[] { "AI motoru geçerli ses döndürmedi. Aktarımı yeniden başlatın.", "The AI engine did not return valid audio. Restart streaming." } },
            { "AiTooSlow", new[] { "AI ses işlemeye yetişemedi; gecikme birikmesini önlemek için aktarım durduruldu.", "AI could not keep up with audio; streaming stopped to prevent accumulating latency." } },
            { "AiBootFailed", new[] { "AI motoru açılamadı. Ayrıntılar: {0}", "The AI engine could not start. Details: {0}" } },
            { "AppTitle", new[] { "Ses Stüdyosu", "Audio Studio" } },
            { "Brand", new[] { "S E S  /  S T Ü D Y O", "A U D I O  /  S T U D I O" } },
            { "Tagline", new[] { "Sesini şekillendir, seslerini paylaş.", "Shape your voice, share your sounds." } },
            { "Language", new[] { "Dil", "Language" } },
            { "Start", new[] { "Aktarımı başlat", "Start streaming" } },
            { "AudioLockedHint", new[] { "Ses efektlerini, AI ses değiştiriciyi ve ses kütüphanesini kullanmak için aktarımı başlatın.", "Start streaming to use voice effects, the AI voice changer and the sound library." } },
            { "Stop", new[] { "Aktarımı durdur", "Stop streaming" } },
            { "Test", new[] { "Test et", "Test audio" } },
            { "Refresh", new[] { "Cihazları yenile", "Refresh devices" } },
            { "Install", new[] { "VB-CABLE kur", "Install VB-CABLE" } },
            { "Uninstall", new[] { "VB-CABLE kaldır", "Remove VB-CABLE" } },
            { "License", new[] { "VB-Audio / VB-CABLE · Bağış ve lisans", "VB-Audio / VB-CABLE · Donations and licensing" } },
            { "Effects", new[] { "Ses efektleri", "Voice effects" } },
            { "Radio", new[] { "Telsiz", "Radio" } },
            { "Cave", new[] { "Mağara", "Cave" } },
            { "Mosquito", new[] { "Sivrisinek", "Mosquito" } },
            { "Reset", new[] { "Sıfırla", "Reset" } },
            { "Equalizer", new[] { "EKOLAYZIR · Bas / Orta / Tiz", "EQUALIZER · Bass / Mid / Treble" } },
            { "Bass", new[] { "EQ Bas: {0} dB", "EQ Bass: {0} dB" } },
            { "Mid", new[] { "EQ Mid: {0} dB", "EQ Mid: {0} dB" } },
            { "Treble", new[] { "EQ Tiz: {0} dB", "EQ Treble: {0} dB" } },
            { "DistortionOff", new[] { "Dogshit Modu: Kapalı (x1)", "Dogshit Mode: Off (x1)" } },
            { "DistortionLow", new[] { "Dogshit Modu: Kötü (x{0})", "Dogshit Mode: Bad (x{0})" } },
            { "DistortionHigh", new[] { "Dogshit Modu: Çok Kötü (x{0})", "Dogshit Mode: Very bad (x{0})" } },
            { "DistortionExtreme", new[] { "Dogshit Modu: KULAK KANATAN (x{0})", "Dogshit Mode: EAR-SPLITTING (x{0})" } },
            { "PitchNormal", new[] { "Ses Tonu: Normal", "Pitch: Normal" } },
            { "PitchLow", new[] { "Ses Tonu: KALIN (Canavar x{0:0.0})", "Pitch: LOW (Monster x{0:0.0})" } },
            { "PitchHigh", new[] { "Ses Tonu: İNCE (Sincap x{0:0.0})", "Pitch: HIGH (Chipmunk x{0:0.0})" } },
            { "PitchRadio", new[] { "Ses Tonu: İNCE (Telsiz Modu x{0:0.0})", "Pitch: HIGH (Radio mode x{0:0.0})" } },
            { "PitchCave", new[] { "Ses Tonu: KALIN (Mağara Modu x{0:0.0})", "Pitch: LOW (Cave mode x{0:0.0})" } },
            { "PitchMosquito", new[] { "Ses Tonu: VIZILTI (🦟 Sivrisinek Modu x{0:0.0})", "Pitch: BUZZ (🦟 Mosquito mode x{0:0.0})" } },
            { "EchoOff", new[] { "Ses Tuzağı (Yankı): Kapalı", "Echo: Off" } },
            { "EchoOn", new[] { "Ses Tuzağı: Açık (~{0} ms Gecikme)", "Echo: On (~{0} ms delay)" } },
            { "Library", new[] { "Ses kütüphanesi", "Sound library" } },
            { "LibraryHint", new[] { "WAV ve MP3 seslerini ekle. Çalmak için çift tıkla veya Enter'a bas.", "Add WAV and MP3 sounds. Double-click or press Enter to play." } },
            { "Search", new[] { "Seslerde ara", "Search sounds" } },
            { "SelectSound", new[] { "Listeden bir ses seçin.", "Select a sound from the list." } },
            { "PlaySound", new[] { "Seçileni çal", "Play selected" } },
            { "AddSound", new[] { "Ses ekle", "Add sounds" } },
            { "RemoveSound", new[] { "Listeden kaldır", "Remove from list" } },
            { "RemoveHint", new[] { "Listeden kaldırmak, bilgisayarındaki ses dosyasını silmez.", "Removing a sound from the list does not delete the file." } },
            { "NoSounds", new[] { "Henüz ses yok · Ses ekle ile başlayın.", "No sounds yet · Choose Add sounds to begin." } },
            { "NoMatches", new[] { "Aramanızla eşleşen ses yok.", "No sounds match your search." } },
            { "SoundCount", new[] { "{0} / {1} ses", "{0} / {1} sounds" } },
            { "FileFilter", new[] { "Ses Dosyaları (*.wav;*.mp3)|*.wav;*.mp3", "Audio files (*.wav;*.mp3)|*.wav;*.mp3" } },
            { "DuplicateTitle", new[] { "Aynı Ses Mevcut", "Sound already added" } },
            { "DuplicateSounds", new[] { "Bazı sesler listede zaten var olduğu için tekrar eklenmedi.", "Some sounds were already in the list and were not added again." } },
            { "SoundMissing", new[] { "Ses dosyası yerinde bulunamadı: {0}", "The sound file could not be found: {0}" } },
            { "SoundLoadError", new[] { "Ses yüklenirken hata oluştu: {0}", "Could not load the sound: {0}" } },
            { "SoundLoading", new[] { "Ses hazırlanıyor: {0}", "Preparing sound: {0}" } },
            { "SoundPlaying", new[] { "Çalınıyor: {0}", "Playing: {0}" } },
            { "SoundFinished", new[] { "Ses tamamlandı.", "Sound finished." } },
            { "SoundStopped", new[] { "Ses durduruldu.", "Sound stopped." } },
            { "LibrarySaveError", new[] { "Kütüphane kaydedilemedi; değişiklik uygulanmadı. Tekrar deneyin. {0}", "The library could not be saved; the change was not applied. Try again. {0}" } },
            { "LibraryLoadError", new[] { "Kütüphane okunamadı. Dosyanızı korumak için düzenleme kapatıldı. Sorunu giderip uygulamayı yeniden açın. {0}", "The library could not be read. Editing is disabled to protect your file. Resolve the issue and restart the app. {0}" } },
            { "LibraryInvalid", new[] { "Kütüphane dosyasındaki ses kayıtları geçersiz.", "The library file contains invalid sound entries." } },
            { "StreamEnded", new[] { "Ses aktarımı sona erdi. Cihazları kontrol edip yeniden başlatabilirsiniz.", "Audio streaming ended. Check your devices and start again." } },
            { "StreamError", new[] { "Ses aktarımı durdu. Cihazları kontrol edip yeniden başlatın. {0}", "Audio streaming stopped. Check your devices and start again. {0}" } },
            { "StreamStopped", new[] { "Ses aktarımı durduruldu.", "Audio streaming stopped." } },
            { "StreamCleanupError", new[] { "Ses kapatılırken hata oluştu: {0}", "Error while closing audio: {0}" } },
            { "Routing", new[] { "Ses yönlendirmesi", "Audio routing" } },
            { "Microphone", new[] { "1 · Kendi mikrofonun", "1 · Your microphone" } },
            { "CableInput", new[] { "2 · Program çıkışı — CABLE Input", "2 · App output — CABLE Input" } },
            { "DiscordHint", new[] { "3 · Discord / diğer uygulamada giriş olarak aynı kablonun CABLE Output ucunu seç", "3 · In Discord / your other app, select the same cable's CABLE Output as the input" } },
            { "CableOutputMissing", new[] { "CABLE Output bulunamadı. Windows ses ayarlarında kayıt aygıtının etkin olduğunu kontrol edin.", "CABLE Output was not found. Check that the recording device is enabled in Windows sound settings." } },
            { "CableMissing", new[] { "VB-CABLE bulunamadı. 'VB-CABLE kur' ile kurun, ardından Windows'u yeniden başlatın.\nVB-CABLE, VB-Audio'nun bağış destekli (donationware) yazılımıdır: www.vb-cable.com", "VB-CABLE was not found. Choose 'Install VB-CABLE', then restart Windows.\nVB-CABLE is donationware by VB-Audio: www.vb-cable.com" } },
            { "RoutingReady", new[] { "Bu program: kendi mikrofonunuz → efektler → CABLE Input. 'Aktarımı başlat' düğmesine basın.\nDiscord / diğer uygulama: giriş = aynı kablonun CABLE Output ucu; çıkış = kulaklığınız.\nInput ve Output aynı sanal kablonun iki ucudur. 'Bu aygıtı dinle' kapalı olmalı.", "This app: your microphone → effects → CABLE Input. Click 'Start streaming'.\nDiscord / other app: input = the same cable's CABLE Output; output = your headphones.\nInput and Output are the two ends of one virtual cable. Keep 'Listen to this device' off." } },
            { "DevicesError", new[] { "Ses cihazları listelenemedi: {0}", "Could not list audio devices: {0}" } },
            { "RecordingDevicesError", new[] { "Kayıt uçları doğrulanamadı. Cihazları yenileyin.", "Recording endpoints could not be verified. Refresh the devices." } },
            { "SelectRouting", new[] { "Kendi mikrofonunuzu ve CABLE Input çıkışını seçin. Kablo görünmüyorsa VB-CABLE kurup cihazları yenileyin.", "Select your microphone and the CABLE Input output. If the cable is missing, install VB-CABLE and refresh the devices." } },
            { "DevicesChanged", new[] { "Ses cihazları değişti. Cihazları yenileyip tekrar seçin.", "Audio devices have changed. Refresh the devices and select them again." } },
            { "StartError", new[] { "Ses başlatılamadı: {0}", "Could not start audio: {0}" } },
            { "PageError", new[] { "Sayfa açılamadı: {0}", "Could not open the page: {0}" } },
            { "Installing", new[] { "VB-CABLE kurucusu hazırlanıyor. Windows izninden sonra 'Install Driver' düğmesine basın.\nKurulum tamamlanınca Windows'u yeniden başlatın.", "Preparing the VB-CABLE installer. After approving the Windows prompt, click 'Install Driver'.\nRestart Windows when installation is complete." } },
            { "Uninstalling", new[] { "VB-CABLE kaldırma: Windows izninden sonra açılan pencerede 'Remove Driver' düğmesine basın.\nBu işlem standart CABLE Input / Output sürücüsünü kaldırır. Ardından Windows'u yeniden başlatın.", "Removing VB-CABLE: after approving the Windows prompt, click 'Remove Driver'.\nThis removes the standard CABLE Input / Output driver. Restart Windows afterwards." } },
            { "Installed", new[] { "Kurucu kapandı (kod: {0}). Kurucudaki sonucu kontrol edin.\nKurulum yaptıysanız Windows'u yeniden başlatın; ardından Discord girişi olarak CABLE Output seçin.", "The installer closed (code: {0}). Check the result shown by the installer.\nIf you installed the driver, restart Windows, then select CABLE Output as the Discord input." } },
            { "Uninstalled", new[] { "Kaldırma penceresi kapandı (kod: {0}). İşlemin sonucunu kurucu bildirir.\nRemove Driver işlemini tamamladıysanız Windows'u yeniden başlatın. Discord'da kendi mikrofonunuzu seçin.", "The removal window closed (code: {0}). The installer reports the result.\nIf you completed Remove Driver, restart Windows. Select your own microphone in Discord." } },
            { "InstallCancelled", new[] { "Yönetici izni iptal edildi. VB-CABLE kurulum işlemi başlatılmadı.", "Administrator approval was cancelled. VB-CABLE installation was not started." } },
            { "UninstallCancelled", new[] { "Yönetici izni iptal edildi. VB-CABLE kaldırma işlemi başlatılmadı.", "Administrator approval was cancelled. VB-CABLE removal was not started." } },
            { "InstallError", new[] { "VB-CABLE kurulum işlemi başlatılamadı: {0}", "Could not start VB-CABLE installation: {0}" } },
            { "UninstallError", new[] { "VB-CABLE kaldırma işlemi başlatılamadı: {0}", "Could not start VB-CABLE removal: {0}" } },
            { "MaintenanceTitle", new[] { "VB-CABLE işlemi devam ediyor", "VB-CABLE operation in progress" } },
            { "CloseInstaller", new[] { "Önce açık VB-CABLE kurulum / kaldırma penceresini kapatın.", "Close the VB-CABLE installation / removal window first." } },
            { "PackageMissing", new[] { "Gömülü VB-CABLE paketi bulunamadı.", "The embedded VB-CABLE package was not found." } },
            { "PackageInvalid", new[] { "VB-CABLE paketinin bütünlük kontrolü başarısız.", "The VB-CABLE package failed its integrity check." } },
            { "PackagePathInvalid", new[] { "Geçersiz paket dosya yolu.", "Invalid package file path." } },
            { "InstallerMissing", new[] { "VB-CABLE kurucusu bulunamadı: {0}", "The VB-CABLE installer was not found: {0}" } },
            { "InstallerOpenError", new[] { "VB-CABLE kurucusu açılamadı.", "Could not open the VB-CABLE installer." } },
            { "EffectChanged", new[] { "Efekt Değişti", "Effect changed" } },
            { "RadioActive", new[] { "📻 Telsiz Modu Aktif!", "📻 Radio mode is active!" } },
            { "CaveActive", new[] { "🕳️ Mağara Modu Aktif!", "🕳️ Cave mode is active!" } },
            { "MosquitoActive", new[] { "🦟 Bzzzz! Sivrisinek Modu Aktif. Kulaklık kullananlara geçmiş olsun!", "🦟 Bzzzz! Mosquito mode is active. Headphone users, brace yourselves!" } },
            { "TestTitle", new[] { "Karşıya giden sesi test et", "Test your outgoing audio" } },
            { "TestHeading", new[] { "Sesini dinle", "Hear your voice" } },
            { "TestDescription", new[] { "AI kadın sesini bağımsız dene veya ana aktarımın sesini dinle.", "Try the AI female voice independently or listen to your main audio stream." } },
            { "AiTestTitle", new[] { "AI kadın sesini dene", "Try the AI female voice" } },
            { "AiTestDescription", new[] { "Ana aktarım kapalıyken mikrofonunu AI kadın sesine dönüştürüp kulaklığından dinle. Ses yalnızca seçtiğin dinleme cihazına gider; VB-CABLE gerekmez.", "With main streaming off, convert your microphone to an AI female voice and hear it in your headphones. Audio goes only to your listening device; VB-CABLE is not required." } },
            { "AiTestDestination", new[] { "Dinleme cihazı · Kulaklık / hoparlör", "Listening device · Headphones / speakers" } },
            { "AiTestVoice", new[] { "Kadın sesi · {0} · Ton: {1} yarım ses", "Female voice · {0} · Pitch: {1} semitones" } },
            { "AiTestStart", new[] { "AI testini başlat", "Start AI test" } },
            { "AiTestPreparing", new[] { "AI hazırlanıyor…", "Preparing AI…" } },
            { "AiTestInputLevel", new[] { "Mikrofon sesi", "Microphone level" } },
            { "AiTestOutputLevel", new[] { "Dinleme cihazına giden ses", "Listening output level" } },
            { "AiTestStop", new[] { "Durdur / İptal", "Stop / Cancel" } },
            { "AiTestReady", new[] { "Mikrofonunu ve kulaklığını seçip AI testini başlat. Ana aktarım kapalı kalır.", "Select your microphone and headphones, then start the AI test. Main streaming stays off." } },
            { "AiTestSelectDevices", new[] { "AI testi için kendi mikrofonunu ve dinleyeceğin kulaklığı / hoparlörü seç.", "Select your microphone and listening headphones / speakers for the AI test." } },
            { "AiTestUnavailable", new[] { "Bağımsız AI testi için önce ana aktarımı ve kablo ses testini durdur; varsa devam eden kurulumu bitir.", "To use the independent AI test, stop main streaming and the cable audio test, and finish any ongoing setup." } },
            { "AiTestMainLocked", new[] { "AI testi açık. Ana aktarımı başlatmak için Test et penceresindeki AI testini durdurun.", "The AI test is active. Stop it in the Test audio window before starting main streaming." } },
            { "AiTestRunning", new[] { "AI testi açık: {0} → Kadın sesi → {1}. Ana aktarım kapalı; ses yalnızca bu cihazdan dinlenir.", "AI test running: {0} → Female voice → {1}. Main streaming is off; audio plays only on this device." } },
            { "AiTestStopped", new[] { "AI testi durduruldu. Ana aktarım kapalı kalır.", "AI test stopped. Main streaming stays off." } },
            { "AiTestInstallHint", new[] { "Motor eksikse ilk testte uygulama içinden indirilip kurulur: 3,3 GB indirme ve yaklaşık 12 GB boş alan. 64 bit Windows ve NVIDIA/CUDA gerekir. Model: Tsukuyomi-chan (CV. Rei Yumesaki).", "If missing, the engine installs in the app on your first test: 3.3 GB download and about 12 GB free space. Requires 64-bit Windows and NVIDIA/CUDA. Model: Tsukuyomi-chan (CV. Rei Yumesaki)." } },
            { "TestDevices", new[] { "Dinleme cihazları", "Listening devices" } },
            { "TestSource", new[] { "1 · Ana pencerede seçtiğin kablonun CABLE Output ucu", "1 · CABLE Output of the cable selected in the main window" } },
            { "TestDestination", new[] { "2 · Sesi duyacağın kulaklık / hoparlör", "2 · Headphones / speakers to listen on" } },
            { "HeadphonesHint", new[] { "Yankıyı önlemek için kulaklık kullan.", "Use headphones to avoid feedback." } },
            { "TestCard", new[] { "Ses testi", "Audio test" } },
            { "TestStart", new[] { "Testi başlat", "Start test" } },
            { "TestStop", new[] { "Testi durdur", "Stop test" } },
            { "TestVolume", new[] { "Dinleme sesi: %{0} (yalnızca bu test)", "Listening volume: {0}% (this test only)" } },
            { "TestDiscordHint", new[] { "Discord'un kendi ses işlemleri bu teste dahil değildir.", "Discord's own audio processing is not included in this test." } },
            { "TestSelectDevices", new[] { "CABLE Output ucunu ve dinleyeceğiniz kulaklığı / hoparlörü seçin.", "Select CABLE Output and the headphones / speakers you want to listen on." } },
            { "TestCableMissing", new[] { "CABLE Output bulunamadı. VB-CABLE kurulumunu ve Windows kayıt aygıtlarını kontrol edin.", "CABLE Output was not found. Check the VB-CABLE installation and Windows recording devices." } },
            { "TestDestinationMissing", new[] { "Kulaklık / hoparlör bulunamadı. Cihazı bağlayıp listeyi yenileyin.", "No headphones / speakers were found. Connect a device and refresh the list." } },
            { "TestReady", new[] { "Kulaklığınızı seçip Testi başlat'a basın. Ana ses akışı gerekirse başlatılır.", "Select your headphones and click Start test. The main audio stream will start if needed." } },
            { "TestMainStopped", new[] { "Ana ses akışı başlamadı. Ana pencerede mikrofon ve CABLE Input seçimini kontrol edin.", "The main audio stream did not start. Check the microphone and CABLE Input selections in the main window." } },
            { "TestRunning", new[] { "Test açık: {0} → {1}.\nKonuşun veya soundboard'dan ses çalın; efektleri ana pencereden değiştirebilirsiniz.", "Test running: {0} → {1}.\nSpeak or play a sound from the library; you can change effects in the main window." } },
            { "TestStartError", new[] { "Test başlatılamadı: {0}", "Could not start the test: {0}" } },
            { "TestEnded", new[] { "Test akışı sona erdi. Yeniden başlatabilirsiniz.", "The test stream ended. You can start it again." } },
            { "TestStoppedError", new[] { "Test durdu: {0}", "The test stopped: {0}" } },
            { "TestStopped", new[] { "Test durduruldu. Ana ses aktarımı bu düğmeyle durdurulmaz.", "Test stopped. This button does not stop the main audio stream." } },
            { "TestCleanupError", new[] { "Test kapatılırken hata: {0}", "Error while closing the test: {0}" } }
        };

        internal static string Get(string key, params object[] arguments)
        {
            string format = messages[key][Language == UiLanguage.English ? 1 : 0];
            var values = Array.ConvertAll(arguments, value => value is Exception ? ((Exception)value).Message : value);
            return string.Format(CultureInfo.GetCultureInfo(Language == UiLanguage.English ? "en-US" : "tr-TR"), format, values);
        }

        internal static T Bind<T>(T control, string key, params object[] arguments) where T : Control
        {
            Set(control, key, arguments);
            return control;
        }

        internal static void Set(Control control, string key, params object[] arguments)
        {
            var message = bindings.GetValue(control, _ => new Message());
            message.Key = key;
            message.Arguments = arguments;
            control.Text = Get(key, arguments);
        }

        internal static void SetLiteral(Control control, string text)
        {
            bindings.Remove(control);
            control.Text = text;
        }

        internal static Exception Error(string key, params object[] arguments)
        {
            return new LocalizedException(key, arguments);
        }

        internal static void Apply(Control root)
        {
            if (root.IsDisposed) return;
            root.SuspendLayout();
            try
            {
                Message message;
                if (bindings.TryGetValue(root, out message)) root.Text = Get(message.Key, message.Arguments);
                foreach (Control child in root.Controls) Apply(child);
            }
            finally { root.ResumeLayout(true); }
            root.Invalidate(true);
        }
    }
}
