# Kadın sesi AI entegrasyonu

Ana sayfanın üstündeki **AI Ses Değiştirici** bölümünde yerel VCClient/RVC motoruyla çalışan dokuz kadın sesi seçeneği bulunur. Sesler ilk modelin alındığı `wok000/vcclient_model` deposundan gelir.

**Ses modeli seç** alanı: Tsukuyomi-chan; Amitaro; Amitaro 16 kHz; Amitaro 16 kHz No F0; AISO Howatto, Kakkoii, Sarasara, Sittori ve Syakitto. Aynı sesin tekrar eden Torch/ONNX kopyaları ve eski eğitim kontrol noktaları ayrı ses olarak listelenmez. Bu katalog mevcut motorun RVC örnek kataloğundaki kadın seslerini kapsar.

Her seçenek boyutunu ve indirilme durumunu gösterir. **Seçili sesi indir** yalnızca o sesin yaklaşık 55–67 MB modelini indirir; ortak motoru veya diğer sesleri tekrar indirmez. Etkinleştirme/test sırasında seçilen ses eksikse otomatik indirilir. İndirme iptal edilip devam ettirilebilir; model dosyası sabit kaynak sürümü ve SHA-256 ile doğrulanmadan kullanılmaz. Ana uygulama ZIP'inde model ağırlıkları bulunmaz.

Ton ayarı No F0 ve AISO modellerinde uygulanmaz. AISO sesleri kaynak önerisine uygun 2040 ms parçalarla çalışır; diğer seçenekler 300 ms parçalar kullanır. AISO seçildiğinde daha yüksek konuşma gecikmesi arayüzde belirtilir. Bir ses çalışırken değiştirmek için AI'ı kapatıp yeni sesi etkinleştir.

Test ekranında aynı ses kataloğu bulunur. Ana aktarım kapalıyken ses indirilebilir ve seçilen ses mikrofon/kulaklık ile denenebilir. Ana ekranın ses seçimi sonraki açılış için saklanır. Yeni kurulumlar modelleri `%LOCALAPPDATA%\SesStudyosu\AiVoices` altında tutar; mevcut `ses_models` kurulumu yeniden indirmeden kullanılabilir.

### Model yönetimi

AI bölümündeki **Model yönetimi**, indirilen ses sayısını ve tüm ses modeli dosyalarının toplam disk alanını gösterir. Yarım kalan indirmeler toplama dahil edilir ve ayrıca belirtilir; ortak AI motoru bu toplamın dışındadır.

Üstteki listeden bir ses seçip **Seçili modeli sil** düğmesine basarak onay verdikten sonra o sesin dosyalarını ve yarım kalan indirmesini kaldırabilirsin. Diğer sesler ve AI motoru korunur. Silinen ses **Seçili sesi indir** ile yeniden kurulabilir. AI aktarımı, test veya indirme sırasında silme düğmesi kilitlenir. Ana aktarım kapalıyken aynı yönetim alanına **Test et** penceresinden ulaşılır. İndirme/silme bittiğinde açık pencerelerin alan bilgisi birlikte güncellenir.

## Kullanım

1. Mikrofon ve CABLE Input seçip **Aktarımı başlat** düğmesine bas. Aktarım başlayınca ses efektleri, AI ses değiştirici ve ses kütüphanesi kullanılabilir. Aktarım durunca bu paneller yeniden kilitlenir. VB-CABLE işlemleri ve **Test et** penceresi aktarım başlamadan da erişilebilirdir.
2. **Ses modeli seç** alanından istediğin sesi seç. **Seçili sesi indir** ile yalnızca modeli indirebilir veya doğrudan **AI sesi etkinleştir** düğmesine basabilirsin. Seçilen model ve ortak motor eksikse uygulama bunları indirir, SHA-256 ile doğrular ve otomatik kurar. İndirme, doğrulama ve kurulum ilerlemesi ana ekranda gösterilir. Ardından motor açılır ve ses modeli hazırlanır. Hazırlık sırasında mevcut ses aktarımı devam eder; model hazır olunca aktarım kısa bir yeniden başlatmayla AI kadın sesine geçer.
3. Discord girişini **CABLE Output** olarak seç. **Test et** ile kulaklıktan dinle.

Ton destekleyen modellerde başlangıç değeri +12 yarım sestir. Değiştirmek için AI'ı kapat, değeri ayarla ve yeniden etkinleştir. No F0 ve AISO seçeneklerinde ton ayarı kilitlenir. AI'ı kapatmak motorun GPU kaynaklarını serbest bırakır ve aktarım klasik ses modunda otomatik devam eder.

### Ana aktarımı açmadan AI testi

**Test et** penceresinin üstündeki **AI kadın sesini dene** panelinde kendi mikrofonunu ve kulaklığını seçip **AI testini başlat** düğmesine bas. Bu test VB-CABLE gerektirmez: mikrofon → AI kadın sesi → seçilen kulaklık yolunu kullanır. Ana aktarım ve ana sayfadaki ses panelleri kapalı kalır. Ayrı dinleme ses seviyesi yalnızca AI testini etkiler.

Bu panelden istediğin ses modelini seçebilirsin. AI motoru veya seçilen model eksikse aynı uygulama içi indirme ve kurulum bu panelden başlar; ilerleme burada gösterilir. Mikrofon ancak model hazır olduğunda açılır. **Durdur / İptal** veya test penceresini kapatmak indirme/hazırlığı ya da dinlemeyi iptal eder. Ana aktarımı başlatmadan önce AI testini durdur. AI testi, destekleyen modellerde ana sayfada seçilmiş tonu ve aynı motoru kullanır; ikinci bir motor açmaz. Ana sayfadaki kayıtlı ses seçimi değişmez. Aynı sesin başarılı testi ana AI hazırlığını korur; farklı bir sesi denedikten sonra ana AI'ı yeniden etkinleştirmek gerekir.

Alt kısımdaki mevcut kablo ses testi, ana aktarımın CABLE Output ucunu dinlemeye devam eder. Bağımsız AI testi ile kablo ses testi aynı anda çalışmaz.

İlk kurulum yaklaşık 3,3 GB indirme ve 12 GB boş alan gerektirir; yardımcı modeller ilk açılışta ayrıca indirilebilir. Bu sürüm 64 bit Windows ve NVIDIA/CUDA motoru için hazırlanmıştır. AI kurmak için ayrı Python kurulumu, PowerShell komutu veya yönetici yetkisi gerekmez. VB-CABLE sürücüsü ayrı bir kurulumdur.

**AI kapat / İptal** ile indirmeyi veya hazırlığı iptal edebilirsin. Hazırlık sırasında **Aktarımı durdur** düğmesi de AI hazırlığını iptal eder ve aktarımı kapatır. Yeniden etkinleştirmek yarım kalan indirmeye devam eder. Bağlantı kesilirse tekrar etkinleştir; doğrulama hatasında bozuk dosya temizlenir ve yeniden indirilir. Kurulum tamamlanmadan motor kullanıma açılmaz.

Akış: mikrofon → AI dönüşümü → mevcut ton/efektler ve soundboard → VB-CABLE. Soundboard dosyaları AI modeline gönderilmez. Türkçe ve İngilizce arayüz desteklenir.

### Görünüm paketleri

**Kişiselleştir → Görünüm paketi** altında iki seçenek bulunur: **Cam · Mevcut tasarım** ve **Klasik · iTunes / iPod**. Klasik paket metal arka plan, gümüş paneller, mavi düğmeler ve LCD başlıkları kullanır. Seçim önce önizlemede görünür; **Kaydet** ile açık pencerelere uygulanır ve sonraki açılış için saklanır. Tema değişimi ses aktarımını yeniden başlatmaz.

Cam paketindeki arka plan görseli, renkler ve cam efekti değeri Klasik pakete geçince saklanır. Klasik paket kendi sabit arka planını kullanır; Cam'a geri dönünce önceki kişiselleştirmeler kullanılmaya devam eder. Kaydet düğmesi yalnızca bir değişiklik varsa görünür.

### Arka planda çalışma ve çıkış

Sağ üst köşedeki **dişli simgesi** Ayarlar penceresini açar. Mikrofon girişini ve ana aktarımın sanal çıkışını burada seçebilirsin. Aktarım, AI testi veya kurulum sürerken cihaz seçimleri kilitlenir; açılış / kapanış tercihleri düzenlenebilir. **Kaydet** değişiklikleri uygular; **Vazgeç** veya ayar penceresinin çarpısı değişiklikleri iptal eder. Seçimler `%LOCALAPPDATA%\SesStudyosu\settings.json` içinde saklanır. Takılı olmayan mikrofon yerine başka bir mikrofon kendiliğinden seçilmez.

Açılış ve kapanış için üç bağımsız seçenek vardır:

- **Çarpıya basınca arka planda çalışmaya devam et:** Varsayılan olarak açık. Kapalıysa ana pencerenin çarpısı uygulamadan tamamen çıkar.
- **Uygulamayı sistem tepsisinde aç:** Varsayılan olarak kapalı. Açıkken sonraki başlatmada pencere gizlenir; mikrofon / aktarım kendiliğinden başlamaz.
- **Windows açılınca uygulamayı otomatik başlat:** Kullanıcı etkinleştirirse geçerli EXE yolu o kullanıcının Windows başlangıcına kaydedilir. Kapatmak bu kaydı kaldırır. Programı başka klasöre taşırsan bu seçeneği yeniden kaydet. Testler gerçek Windows başlangıç kaydını değiştirmez.

Arka planda çalışma açıkken ana pencerenin **X** düğmesi uygulamayı saat yanındaki sistem tepsisine gizler; ses aktarımı ve AI testi çalışmaya devam eder. Uygulamayı geri açmak için tepsi simgesine çift tıkla veya sağ tıklayıp **Uygulamayı göster** seçeneğini kullan. Tepsi menüsünde **Ayarlar** da bulunur. Açık test ve kişiselleştirme pencereleri durumları korunarak geri gelir.

**Aktarımı durdur** düğmesinin yanındaki **Uygulamayı kapat** düğmesi ses işlemlerini ve AI motorunu durdurup uygulamadan tamamen çıkar. Sistem tepsisi menüsünde de aynı çıkış seçeneği bulunur. VB-CABLE kurulum penceresi açıksa tam çıkıştan önce bu pencereyi kapat.

## Çalışma düzeni

- .NET Framework 4.8 / NAudio arayüzü, VCClient 1.5.3.18a CUDA motorunu gizli arka plan işlemi olarak başlatır.
- Otomatik kurulum `%LOCALAPPDATA%\SesStudyosu\AiRuntime` altındadır. Sonraki açılışlarda bu kurulum kullanılır; motor tekrar indirilmez. Uygulamanın yanındaki veya geliştirmede proje kökündeki mevcut `AiRuntime` klasörü de tanınır. Kullanıcı kurulumu önceliklidir.
- Bağlantı `https://127.0.0.1:18991` üzerinden yapılır. İstemci yalnızca başlattığı motorun ürettiği sertifikayı kabul eder; genel sertifika doğrulaması kapatılmaz.
- Ses 44.1 kHz, mono, 16 bit PCM olarak modele göre 300 veya 2040 ms parçalar halinde işlenir. Kuyruk iki bekleyen parçayla sınırlıdır. Motor yetişemezse aktarım hata mesajıyla durur; işlenmemiş mikrofon sesine kendiliğinden geçmez.
- Her model kendi numaralı alt klasöründe tutulur: yeni kurulumlarda `%LOCALAPPDATA%\SesStudyosu\AiVoices`, mevcut kurulumlarda `ses_models`. Hazırlama sırasında kısa sentetik sesle GPU ısıtılır ve ardından sessizlikle temizlenir.
- Seçilen kadın sesi uygulama tarafından indirilir ve doğrulanır; seçilmeyen sesler indirilmez. Motor ilk açılışta ortak yardımcı modelleri indirebilir ve örnek model kataloğunu kontrol eder.
- Motor günlüğü: `AiRuntime/MMVCServerSIO/ses-ai.log`.

## Kurulum ve derleme

Uygulamayı başka bilgisayara taşırken `bin\Release` içindeki `ses uyg.exe`, `ses uyg.exe.config` ve tüm DLL dosyalarını birlikte dağıt. AI motorunu EXE'ye gömmek veya elle kopyalamak gerekmez; kullanıcı uygulamada ilk kez etkinleştirdiğinde kurulur. Hedef bilgisayarda .NET Framework 4.8 bulunmalıdır.

Derleme ve dağıtım paketi:

```powershell
msbuild "ses uyg.csproj" /t:Build /p:Configuration=Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-App.ps1 -SkipBuild
```

Paket `dist\Ses-Studyosu.zip` olarak üretilir. ZIP'i bir klasöre çıkartıp `ses uyg.exe` dosyasını aç. `tools/Install-AiRuntime.ps1` geliştiriciler için isteğe bağlı olarak korunmuştur; son kullanıcı bu betiğe ihtiyaç duymaz. `installed.txt`, gerekli motor dosyalarıyla birlikte kurulumun tamamlandığını belirtir.

## Doğrulama

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-Checks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-Checks.ps1 -AiRuntime
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-Checks.ps1 -AiVoices
```

İlk komut ses işleme, arayüz, model deposu ve AI kurulum testlerini çalıştırır. Kurulum testleri küçük yerel HTTP paketleriyle yeni kurulum, devam ettirme, iptal, hatalı paket, eşzamanlı kurulum ve motor bulmayı sınar; gerçek motor indirilmez veya çalıştırılmaz. İkinci komut kurulu ilk modeli fiziksel mikrofonu açmadan sentetik sesle çalıştırır. Üçüncü komut dokuz modeli indirip doğrular, her biriyle sentetik ses dönüşümünü ve model değiştirmeyi sınar. RTX 4060 Laptop GPU üzerindeki kontrolde 300 ms parçalar için 30–54 ms, AISO'nun 2040 ms parçaları için 92–104 ms işlem süresi elde edildi. Bunlar uçtan uca gecikme ölçümleri değildir. Gerçek konuşma kalitesi ve oyun açıkken gecikme kullanıcı mikrofonuyla ayrıca değerlendirilmelidir.

## Kaynak ve model koşulları

- [VCClient kaynak kodu ve lisansları](https://github.com/w-okada/voice-changer/tree/v.1.5.3.18a)
- [Resmi CUDA paketi](https://huggingface.co/wok000/vcclient000)
- [Tsukuyomi-chan model koşulları](https://huggingface.co/wok000/vcclient_model/blob/main/rvc_v2_alpha/tsukuyomi-chan/terms_of_use.txt)
- [Tsukuyomi-chan ses korpusu — CV. Rei Yumesaki](https://tyc.rei-yumesaki.net/material/corpus/)
- [Amitaro model koşulları](https://huggingface.co/wok000/vcclient_model/blob/main/rvc_v2_alpha/amitaro/terms_of_use.txt)
- [Amitaro 16 kHz model koşulları](https://huggingface.co/wok000/vcclient_model/blob/main/rvc_v2_alpha/amitaro16k/terms_of_use.txt)
- [AISO sesleri, kullanım bilgileri ve lisansı](https://huggingface.co/wok000/vcclient_model/blob/main/rvc_v2_chihaya_jinja/description.txt)

Model VCClient motoru üzerinden kullanılır. Model ve ses verisi, motor kodundan ayrı kullanım koşullarına sahiptir; model koşulları arayüzdeki bağlantıdan açılabilir. Bu entegrasyon yeni bir model eğitmez.
