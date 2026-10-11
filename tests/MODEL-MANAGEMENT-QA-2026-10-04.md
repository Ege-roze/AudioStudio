# Model yönetimi doğrulaması — 4 Ekim 2026

Ana AI bölümü ve bağımsız test ekranına indirilen ses sayısı, toplam model alanı ve seçili modeli silme eklendi. Yarım kalan indirmeler toplamda ayrıca açıklanır; ortak motor hesap dışındadır. Silme onay ister ve yalnızca seçilen katalog modelinin ağırlık, metadata ve kısmi indirme dosyalarını kaldırır.

Release derlemesi başarılı. Açık Debug uygulaması kapatılmadı veya değiştirilmedi.

Geçen kontroller:

- 190 ses/kütüphane, 12 ayar, 75 yerelleştirme ve arayüz kontrolü.
- 24 kişiselleştirme ve 8 tema entegrasyon kontrolü.
- AI kuyruk, iptal ve hata iletimi regresyonları; 49 motor kurulum kontrolü.
- 37 model deposu kontrolü: alan hesabı, yarım indirme temizliği, iptal, kullanım kilidi, dosya kilidi hatası, yabancı model koruması, yeniden indirme ve ilk ses silinince eski deponun bulunması.
- 9 canlı model yönetimi arayüz kontrolü: iki pencerede eşzamanlı güncelleme, işlem kilidinin korunması, yeniden indirme düğmesi, dar pencere ve aboneliklerin temizlenmesi.
- 8 gömülü/yayıma hazırlanan koşul belgesi kontrolü ve yerel bağlantı gizlilik testi.
- Kurulu Tsukuyomi-chan ile 15 sentetik ses bloğunda gerçek dönüşüm: 300 ms blok başına ortalama 51 ms. Mikrofon/hoparlör açılmadı; motor test sonunda kapandı.

HTTP dinleyici testi korumalı ortamda desteklenmediği, motor testi de ortamın PATH/Path çakışmasına takıldığı için bu iki test normal izinlerle yeniden çalıştırıldı ve geçti. Diğer testler korumalı ortamda geçti.

Silme kontrolleri yalnızca oluşturulan küçük test dosyalarında yapıldı. Mevcut dokuz gerçek model korunuyor; ağırlıkların toplamı 519,5 MB. Türkçe test paneli görseli `previews/test-ai-idle-tr.png` dosyasında.

`dist/Ses-Studyosu.zip` güncellendi (yaklaşık 3,18 MB). ZIP içindeki EXE'nin SHA-256 değeri Release derlemesiyle eşleşiyor; pakette motor, ses ağırlıkları veya test çalıştırılabilir dosyaları yok.

Tekrar çalıştırma: `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Run-Checks.ps1 -Configuration Release -Render`. Motor testi için `-AiRuntime` eklenebilir.
