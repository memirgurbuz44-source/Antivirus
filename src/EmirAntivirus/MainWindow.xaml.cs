using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using EmirAntivirus.Services;
using Microsoft.Win32;

namespace EmirAntivirus;

public partial class MainWindow : Window
{
    private readonly SignatureScanner _scanner = new();
    private readonly ObservableCollection<ActivityEntry> _activity = new();

    public MainWindow()
    {
        InitializeComponent();
        ActivityList.ItemsSource = _activity;
        AddActivity("Başlatıldı", "Emir Antivirus hazır. Yerel imza listesi boş.");
        ProtectionStatus.Text = "İMZA LİSTESİ BEKLEMEDE";
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Taranacak klasörü seçin" };
        if (dialog.ShowDialog() == true)
            await ScanDirectoryAsync(dialog.FolderName);
    }

    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        var paths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"
        };
        foreach (var path in paths.Where(Directory.Exists))
            await ScanDirectoryAsync(path);
    }

    private async void FullScan_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Tam tarama uzun sürebilir. Sistem sürücüsünü taramak istiyor musunuz?",
            "Tam tarama", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
            await ScanDirectoryAsync(Path.GetPathRoot(Environment.SystemDirectory)!);
    }

    private async Task ScanDirectoryAsync(string folder)
    {
        try
        {
            StatusText.Text = $"Taranıyor: {folder}";
            var checkedCount = 0;
            var findings = 0;
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var finding = await _scanner.ScanFileAsync(file);
                    checkedCount++;
                    if (finding is not null)
                    {
                        findings++;
                        AddActivity("İmza eşleşmesi", finding.FilePath);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            StatusText.Text = $"Tarama tamamlandı. {checkedCount} dosya kontrol edildi; {findings} imza eşleşmesi bulundu.";
            AddActivity("Tarama tamamlandı", $"{checkedCount} dosya • {findings} eşleşme");
            if (findings == 0)
                MessageBox.Show("Yerel imza listesinde eşleşme bulunmadı. Bu sonuç, cihazın kesin olarak temiz olduğu anlamına gelmez.",
                    "Tarama sonucu", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Tarama tamamlanamadı.";
            AddActivity("Hata", ex.Message);
        }
    }

    private void Quarantine_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Karantina yöneticisi sonraki geliştirme aşamasında eklenecek.",
            "Karantina", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Network_Click(object sender, RoutedEventArgs e) =>
        new NetworkConnectionsWindow { Owner = this }.Show();

    private void History_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show($"Bu oturumdaki { _activity.Count } etkinlik kaydı görüntüleniyor.",
            "Tarama geçmişi", MessageBoxButton.OK, MessageBoxImage.Information);

    private void AddActivity(string action, string details) =>
        _activity.Insert(0, new ActivityEntry(DateTime.Now.ToString("HH:mm:ss"), action, details));

    public sealed record ActivityEntry(string Time, string Action, string Details);
}
