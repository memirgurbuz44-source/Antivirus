# Emir Antivirus

Windows için geliştirilen, kullanıcı denetimli güvenlik uygulaması.

## Özellik hedefleri
- WPF tabanlı Türkçe kontrol paneli
- Hızlı, tam, derin ve özel klasör/dosya taraması
- Tarama ilerlemesi, iptal düğmesi ve erişilemeyen dosya sayacı
- Derin taramada dosyaları çalıştırmadan başlık/imza incelemesi
- Ağ taraması penceresinde mevcut TCP bağlantılarını listeleme
- SHA-256 tabanlı imza eşleştirme altyapısı
- Gerçek zamanlı dosya değişikliği izleme
- Kullanıcı onaylı karantina ve geri yükleme
- Tarama geçmişi ve raporlar
- Dosya/klasör istisnaları
- Zamanlanmış tarama
- İmzalar ve uygulama için güvenli güncelleme altyapısı
- Ayarlar, bildirimler ve çevrimdışı çalışma
- İnternet gerektirmeyen yerel dosya taraması
- Windows TCP bağlantı tablosundan pasif ağ bağlantısı gözlemi
- Program adı, PID, yerel/uzak uç nokta ve bağlantı durumu görünümü

## Güvenlik ilkeleri
- Dosyalar kullanıcı izni olmadan silinmez veya değiştirilmez.
- Karantina geri alınabilir olmalıdır.
- Uygulama kendini gizlemez, güvenlik yazılımlarını kapatmaz ve sistem ayarlarını izinsiz değiştirmez.
- İmza veritabanı boşsa uygulama bunu açıkça belirtir; tarama sonucu “temiz” ifadesi mutlak güvence olarak sunulmaz.
- Derin tarama dosyaları çalıştırmaz veya arşivleri açmaz; yalnızca yerel hash imzalarını ve sınırlı dosya başlık bilgisini kontrol eder.
- Şüpheli dosyalar otomatik çalıştırılmaz; içerikleri analiz için yürütülmez.
- Ağ ekranı yalnızca Windows tarafından raporlanan etkin TCP bağlantılarını listeler. HTTPS/TLS içeriğini, gönderilen istek gövdelerini veya geçmiş DNS sorgularını çözmez.
- Genel IP adresi tek başına zararlı olarak sınıflandırılmaz. Bu ilk sürümde çevrimdışı tarama ağ üzerinden imza indirmez veya dosya yüklemez.

## Geliştirme
Visual Studio 2022 veya .NET 8 SDK ile `src/EmirAntivirus/EmirAntivirus.csproj` dosyasını açın. Windows üzerinde çalıştırın.

> Bu proje geliştirme aşamasındadır. Henüz profesyonel bir antivirüsün yerini almaz.
