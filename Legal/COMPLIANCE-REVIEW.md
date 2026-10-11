# Ses Stüdyosu inceleme ve yayın durumu

İnceleme tarihi: 27 Eylül 2026. İncelenen çözüm, Visual Studio otomasyonundan doğrulanan `ses uyg.sln` dosyasıdır. Aktif Form1.cs başlangıçta kaydedilmişti. Bu rapor otomatik Microsoft Store sertifikasyonu veya bütün ülkeler için hukuki uygunluk onayı değildir.

## İncelenen davranış

| Konu | Bulgular |
|---|---|
| Tür | C# WinForms, .NET Framework 4.8, klasik MSBuild projesi, AnyCPU. MSIX/UWP manifesti veya Store paketleme projesi yok. |
| Mikrofon | Kullanıcı başlatınca NAudio WaveInEvent, mono 44.1 kHz / 16 bit. Klasik akış 100 ms, AI akışı 300 ms tamponlar. |
| Ses | Klasik efektler, EQ ve seçilen yerel ses dosyaları bellek içinde işlenip VB-CABLE girişine yönlendirilir. Media Foundation yeniden örnekleme kullanılır. Bağımsız AI testi seçilen kulaklık/çıkışa gider. |
| AI | VCClient v.1.5.3.18a, Tsukuyomi-chan RVC modeli, rmvpe_onnx; ayrı süreç. Ses HTTP isteğinin JSON gövdesinde Base64 PCM olarak, HTTPS 127.0.0.1:18991 üzerinden gider. Sertifika eşleştirmesi ve proxy bypass mevcut. |
| Dış ağ | Motor paketi Hugging Face; motor/model katalogları ve eksik yardımcı ağırlıklar Hugging Face/CDN; eksik Whisper ağırlığı openaipublic.azureedge.net. Motor her başlangıçta örnek katalogları yenileyebilir. Kullanıcı tarafından açılan kaynak bağlantıları tarayıcıya gider. |
| Yerel sunucu | VCClient'ın sabit sürüm kaynağında HTTPS sunucusu `host="0.0.0.0"`, açık CORS ve dosya/ayar uçları vardır; ayrı kimlik doğrulaması yok. İstemcinin localhost kontrolü sunucuyu izole etmez. |
| Hesap/telemetri | Ana uygulamada hesap, ödeme, reklam, analitik veya otomatik raporlama SDK'sı bulunmadı. Bu bulgu Windows/Store/harici sitelerin kendi işlemleri hakkında garanti değildir. |
| Kalıcılık | LocalAppData/SesStudyosu ayarları, kopyalanmış arka plan resmi; exe yanında soundboard_config.xml; ayrı AI runtime/modeller/kataloglar/günlükler/temp/sertifikalar; isteğe bağlı HKCU Run başlangıç girdisi. |
| Çıkış | X varsayılan olarak tepsiye gizler; ses devam edebilir. Tam çıkış veya uygun Durdur eylemi gerekir. Mevcut davranış metinde açıklandı. |
| Arayüz | Kaynaklar ve Haklar ekranında yalnızca özetler/harici lisans bağlantıları vardı; tam kullanım/gizlilik/attribution belgeleri eksikti. |

## Uygulanan sınırlı değişiklikler

- Türkçe/İngilizce Privacy Policy, Terms of Service, Third-Party Notices ve model koşulları eklendi. Aynı üretilen TXT dosyaları uygulamaya gömülür ve statik HTML/TXT olarak dışa aktarılır.
- Ana ekrandan mikrofon açılmadan önce, Ayarlar'dan ve Kaynaklar ve Haklar'dan belgeler okunabilir. AI ekranları indirme sunucuları ve mevcut ağ sınırlamasını açıklar. Genel kullanım için yeni bir zorunlu onay, hesap veya telemetri eklenmedi.
- 15 NuGet paketinin sürüm ve lisans envanteri çıkarıldı. MIT metinleri, NAudio içindeki Bernsee WOL atfı, VB-CABLE readme, VCClient modül lisansları, GPLv3 ve özgün model şartları korundu. İsteğe bağlı motorun bulunan lisans dosyaları ayrıca kopyalandı.
- AI hazırlanırken `recordIO=0` açıkça ayarlanır; uygulamanın kayıt yapmayan akışı, motorun önceki kayıt ayarına bırakılmaz. Yerel ses istekleri HTTP yönlendirmelerini takip etmez.
- Motorun yalnızca yerel IP tespiti için kullandığı `--test_connect` parametresi 127.0.0.1 yapıldı. **Bu parametre sunucunun dinlediği adresi değiştirmez.**
- Yayın ZIP'i belgeleri ve lisansları içerir; gömülü metin ile dış metin farklıysa paketleme durur. Ana paket motor veya model ağırlıklarını içermez. Mikrofon/efekt yönlendirmesi, cihaz seçimi ve normal başlat/durdur akışı korunmuştur.

## Yayından önce açık kalan somut konular

1. **Yayıncı ve iletişim:** Kullanıcı şirketi olmadığını ve iletişim e-postasını daha sonra oluşturacağını belirtti. Ad/e-posta uydurulmadı. `publisher.json` doldurulmadığından belgeler açıkça taslaktır. İsim, e-posta ve yürürlük tarihi tamamlanıp belgeler yeniden üretilmelidir. HTTPS alanı verilmedi; dosyalar internete yüklenmedi. Partner Center'a gerçek, herkese açık gizlilik URL'si girilmelidir.

2. **AI motoru ağ izolasyonu:** Sunucu bütün IPv4 arayüzlerinde dinlemeye devam eder; uygulama istemcisinin yönlendirme önlemi bunu çözmez. Dağıtım öncesi, motorun yalnızca loopback'te dinlediği ve yetkisiz yerel/web istemcilerinden korunmuş bir sürüm/integrasyon hazırlanıp test edilmelidir. Sabit sürümün komut satırında desteklenmeyen bir `--host` seçeneği eklenmemiş, mevcut motor binary'si veya kullanıcının sistem güvenlik duvarı sessizce değiştirilmemiştir. LAN erişimi ve motor kayıt/ayar uçları gerçek bir inceleme bulgusudur; yalnızca politika metniyle giderilmiş sayılmaz.

3. **AI lisanslarının dağıtım kapsamı:** VCClient kod lisansını bütün pakete yaymak yanlıştır. ContentVec ONNX / RMVPE ONNX kaynağı `wok000/weights_gpl` modelleri GPLv3 olarak işaretler. Tsukuyomi-chan özel koşulları modeli yalnızca VCClient üzerinde kullanımla sınırlar. Bu entegrasyonun ve planlanan dağıtımın hakları doğrulanmalıdır; ana uygulama keyfî biçimde GPL olarak yeniden lisanslanmadı. İsteğe bağlı motorun `LICENSE-BeatriceJVS` metni ticari kullanımı yasaklar ve yeniden dağıtım için izin gerekliliğini belirtir; entegrasyonun bu modeli seçmemesi paketin tamamı için izin bulunduğunu göstermez. CUDA/cuDNN, FFmpeg/Chromium, libsndfile ve diğer binary'lerin dağıtım/source gereklilikleri bir notices dosyasıyla kendiliğinden çözülmez. Bu motorun bütün bağımlılıkları için ticari/yeniden dağıtım izni onaylanmış değildir. Gerekirse dağıtım hakları doğrulanmış daha dar bir runtime/model paketi hazırlanmalıdır.

4. **Store paketleme seçimi:** Bu çalışmada MSIX manifesti, sertifika, mağaza kaydı veya yükleme oluşturulmadı. Win32 MSI/EXE yolu ile MSIX yolu farklı gereksinimler doğurur. Örneğin exe yanına yazılan soundboard_config.xml ve yönetici onayıyla sürücü kuran akış, MSIX'in korumalı kurulum dizini/dağıtım modeli için ayrıca ele alınmalıdır. Paket türü seçilmeden gereksiz capability veya UWP izin istemi eklenmedi. Yayınlanacak gerçek pakette cihazlar, mikrofon izin hataları, sürücü kurulumu ve kurulum/kaldırma davranışı ayrıca sertifikasyon öncesi test edilmelidir.

## Dayanaklar

- Microsoft Store Policies 7.20, özellikle 10.5.1 (Win32 dahil gizlilik politikası) ve 10.5.4: https://learn.microsoft.com/en-us/windows/apps/publish/store-policies-and-code-of-conduct
- Microsoft bireysel/şirket kayıt seçenekleri: https://developer.microsoft.com/en-us/microsoft-store/register
- VB-CABLE dağıtımı/bağış görünürlüğü: https://vb-audio.com/Services/licensing.htm
- VCClient sabit sürüm kaynağı: https://github.com/w-okada/voice-changer/tree/v.1.5.3.18a
- Ağ dinleme/katalog başlangıcı: https://github.com/w-okada/voice-changer/blob/v.1.5.3.18a/server/MMVCServerSIO.py
- CORS/dosya uçları: https://github.com/w-okada/voice-changer/blob/v.1.5.3.18a/server/restapi/MMVC_Rest.py
- GPL model kartı: https://huggingface.co/wok000/weights_gpl
- Model koşulları: https://huggingface.co/wok000/vcclient_model/blob/main/rvc_v2_alpha/tsukuyomi-chan/terms_of_use.txt
- NAudio sürüm lisansı: https://github.com/naudio/NAudio/blob/v2.3.0/license.txt
- NAudio pitch atfı: https://github.com/naudio/NAudio/blob/v2.3.0/NAudio.Core/Dsp/SmbPitchShifter.cs

Envanter, proje kaynakları ve mevcut paket içerikleri üzerinden yapıldı. Ana uygulamada özellik bulunmaması, bütün üçüncü taraf binary'lerin her olası çalışma yolunun tersine mühendislikle doğrulandığı anlamına gelmez. Bulgular ile doğrulanmamış noktalar yukarıda ayrılmıştır.
