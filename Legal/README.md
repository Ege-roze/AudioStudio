# Hukuki metinler

Şirket zorunlu değildir. `publisher.json` içindeki `publisherName` alanına gerçek bireysel yayıncı/geliştirici kimliğini, `contactEmail` alanına kullanılabilir destek/gizlilik e-postasını girin. Mağaza yayıncı bilgileriyle tutarlı olmalıdır. İki alan boşken dosyalar açıkça taslak olarak üretilir.

`tools/Export-LegalDocuments.ps1` Türkçe ve İngilizce metinleri üretir. `-ForPublication` eksik yayıncı veya e-posta varsa durur. Bu kontrol yalnızca belge bilgileridir; uygulamanın Microsoft Store tarafından kabul edileceği anlamına gelmez. Yayın öncesi teknik/lisans açıkları `COMPLIANCE-REVIEW.md` içinde kayıtlıdır.

- `templates/`: düzenlenecek metinler. Uygulama davranışı değiştiğinde güncelleyin.
- `*.txt`: uygulamanın içine gömülen, çevrimdışı okunan metinler.
- `site/`: HTTPS statik barındırmaya yüklenecek HTML ve TXT dosyaları. JavaScript, analitik, çerez, dış font veya uzaktan yüklenen görsel yoktur.
- `licenses/`: özgün üçüncü taraf metinleri; ana lisanslar bildirim belgesine de eklenir. VB-CABLE readme metni içerik değiştirilmeden Windows-1252'den UTF-8'e çevrilmiştir. Diğer metinler özgün kaynaklardan korunur.
- `dependency-inventory.json`: NuGet sürümleri, paket SHA-256 değerleri ve mevcut isteğe bağlı motorun lisans dosyası envanteri. Tam bir binary/model dağıtım izni belgesi değildir.

Yayıncı bilgileri tamamlanınca dışa aktarımı `-ForPublication` ile çalıştırın, uygulamayı yeniden derleyin ve `site` dosyalarını seçtiğiniz HTTPS alanına yükleyin. Örneğin kendi gerçek alanınızda `/privacy-policy.tr.html` veya `/privacy-policy.en.html` adresinin herkese açık, oturum açmadan ve indirme zorunluluğu olmadan açıldığını doğrulayın. Gerçek gizlilik URL'sini Partner Center'a girin. Henüz bir alan adı seçilmediğinden kodda sahte URL yoktur ve internet yayını yapılmamıştır.

`tools/Publish-App.ps1` dışa aktarımı çalıştırır, belgeleri ve lisansları ana dağıtım ZIP'ine ekler. `-ForPublication` boş kimlik alanlarını reddeder; `-SkipBuild` kullanıldığında da gömülü metinlerle dışa aktarılan metinlerin birebir aynı olduğunu denetler. Motor/model ağırlıkları ZIP'e eklenmez.

Belge görüntüleme için internet veya ek kullanıcı hesabı gerekmez. Genel kullanım koşulları için zorunlu onay ekranı veya mikrofonu kendiliğinden açan bir işlem eklenmemiştir. Başlatma/durdurma davranışı korunur.
