# Emir Antivirus

Windows için geliştirilen, kullanıcı denetimli bir güvenlik uygulaması prototipi.

## Özellikler
- Hızlı tarama: Masaüstü, İndirilenler, Belgeler ve geçici klasör.
- Tam tarama: Erişilebilir sabit disklerdeki dosyalar.
- Derin tarama: SHA-256 özeti, dosya başlığı/türü ve boyut kontrolü; başlık/uzantı uyuşmazlığı için elle inceleme uyarısı.
- Dosya/klasör seçerek tarama, iptal ve sayaçlar.
- Mevcut TCP bağlantılarını gösteren ağ penceresi.
- GitHub Releases sürüm kontrolü; yeni sürüm varsa uygulamada güncelleme düğmesi.
- GitHub Actions ile Windows x64 tek dosya EXE üretimi.

## EXE derleme
Her main dalı push işleminde GitHub Actions Windows x64 self-contained EXE üretir. Depodaki Actions sekmesinden başarılı çalışmanın EmirAntivirus-win-x64 artifact'ını indirebilirsiniz.

Yeni sürüm yayımlamak için csproj içindeki sürüm numarasını artırıp v1.0.1 gibi bir Git etiketi gönderin. İş akışı GitHub Release oluşturur ve EXE dosyasını ekler. Uygulamadaki güncelleme düğmesi Release sayfasını açar; kullanıcı güncellemeyi kendisi indirir.

## Sınırlamalar
- Yerel SHA-256 imza listesi henüz doldurulmadı; eşleşme bulunmaması temiz olduğu anlamına gelmez.
- Derin tarama gelişmiş antivirüs motoru, davranış analizi veya bulut itibar hizmeti değildir.
- Başlık/uzantı uyuşmazlığı tek başına zararlı yazılım kanıtı değildir.
- Ağ ekranı yalnızca etkin TCP bağlantılarını listeler.
- Uygulama dosyaları çalıştırmaz, otomatik silmez veya değiştirmez. Microsoft Defender'ın yerine geçmez.

## Yerel geliştirme
Windows üzerinde .NET 8 SDK ile dotnet publish komutu kullanılarak yayımlanabilir.
