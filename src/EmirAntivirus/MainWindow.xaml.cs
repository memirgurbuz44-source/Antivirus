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
    private CancellationTokenSource? _scanCancellation;

    public MainWindow()
    {
        InitializeComponent();
        ActivityList.ItemsSource = _activity;
        AddActivity("Başlatıldı", "Hazır. Dosya taraması çevrimdışı çalışır; yerel imza listesi boş.");
        ProtectionStatus.Text = "YEREL TARAMA HAZIR";
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Taranacak klasörü seçin" };
        if (dialog.ShowDialog() == true)
            await RunScanAsync(new[] { dialog.FolderName }, deep: true, "Özel klasör taraması");
    }

    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(user, "Downloads"),
            Path.Combine(user, "Documents"),
            Path.Combine(user, "AppData", "Local", "Temp")
        };
        await RunScanAsync(paths.Where(Directory.Exists), deep: false, "Hızlı tarama");
    }

    private async void FullScan_Click(object sender, RoutedEventArgs e)
    {
        var roots = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName)
            .ToArray();
        if (roots.Length == 0)
        {
            MessageBox.Show("Taranabilir sabit disk bulunamadı.");
            return;
        }
        await RunScanAsync(roots, deep: false, "Tam tarama");
    }

    private async void DeepScan_Click(object sender, RoutedEventArgs e)
    {
        var roots = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName)
            .ToArray();
        if (roots.Length == 0)
        {
            MessageBox.Show("Taranabilir sabit disk bulunamadı.");
            return;
        }
        var answer = MessageBox.Show(
            "Derin tarama tüm erişilebilir sabit diskleri kontrol eder ve daha uzun sürebilir. Devam edilsin mi?",
            "Derin tarama", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes)
            await RunScanAsync(roots, deep: true, "Derin tarama");
    }

    private async Task RunScanAsync(IEnumerable<string> roots, bool deep, string scanName)
    {
        if (_scanCancellation is not null)
            return;

        _scanCancellation = new CancellationTokenSource();
        var token = _scanCancellation.Token;
        CancelButton.IsEnabled = true;
        ScanProgress.Value = 0;
        FilesCountText.Text = "0";
        FindingsCountText.Text = "0";
        SkippedCountText.Text = "0";
        ProtectionStatus.Text = "TARAMA ÇALIŞIYOR";

        long checkedCount = 0;
        long findings = 0;
        long skipped = 0;
        var stack = new Stack<string>(roots.Where(Directory.Exists).Reverse());

        try
        {
            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var directory = stack.Pop();

                try
                {
                    foreach (var child in Directory.EnumerateDirectories(directory))
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            var attributes = File.GetAttributes(child);
                            if ((attributes & FileAttributes.ReparsePoint) == 0)
                                stack.Push(child);
                        }
                        catch (UnauthorizedAccessException) { skipped++; }
                        catch (IOException) { skipped++; }
                    }
                }
                catch (UnauthorizedAccessException) { skipped++; }
                catch (IOException) { skipped++; }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory))
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            var finding = await _scanner.ScanFileAsync(file, token);
                            checkedCount++;

                            if (deep)
                            {
                                // Read a small header only; never execute or unpack the file.
                                await InspectHeaderAsync(file, token);
                            }

                            if (finding is not null)
                            {
                                findings++;
                                AddActivity("İmza eşleşmesi", $"{finding.DetectionName} • {finding.FilePath}");
                            }
                        }
                        catch (UnauthorizedAccessException) { skipped++; }
                        catch (IOException) { skipped++; }
                        catch (System.Security.SecurityException) { skipped++; }

                        if (checkedCount % 20 == 0)
                        {
                            FilesCountText.Text = checkedCount.ToString("N0");
                            FindingsCountText.Text = findings.ToString("N0");
                            SkippedCountText.Text = skipped.ToString("N0");
                            StatusText.Text = $"{scanName}: {checkedCount:N0} dosya kontrol edildi...";
                            await Task.Yield();
                        }
                    }
                }
                catch (UnauthorizedAccessException) { skipped++; }
                catch (IOException) { skipped++; }
            }

            ScanProgress.Value = 100;
            StatusText.Text = $"{scanName} tamamlandı. {checkedCount:N0} dosya, {findings:N0} imza eşleşmesi, {skipped:N0} erişilemeyen öğe.";
            ProtectionStatus.Text = "TARAMA TAMAMLANDI";
            AddActivity(scanName, StatusText.Text);
            if (findings == 0)
                MessageBox.Show("Yerel imza listesinde eşleşme bulunmadı. İmza listesi boş veya eksik olabilir; bu sonuç cihazın kesin temiz olduğunu kanıtlamaz.",
                    "Tarama sonucu", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = $"Tarama iptal edildi. {checkedCount:N0} dosya kontrol edildi.";
            ProtectionStatus.Text = "TARAMA İPTAL EDİLDİ";
            AddActivity("İptal", StatusText.Text);
        }
        finally
        {
            FilesCountText.Text = checkedCount.ToString("N0");
            FindingsCountText.Text = findings.ToString("N0");
            SkippedCountText.Text = skipped.ToString("N0");
            CancelButton.IsEnabled = false;
            _scanCancellation.Dispose();
            _scanCancellation = null;
        }
    }

    private static async Task<string> InspectHeaderAsync(string path, CancellationToken token)
    {
        var buffer = new byte[16];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
        if (read >= 2 && buffer[0] == 0x4D && buffer[1] == 0x5A) return "Windows PE";
        if (read >= 4 && buffer[0] == 0x50 && buffer[1] == 0x4B) return "ZIP tabanlı";
        if (read >= 4 && buffer[0] == 0x25 && buffer[1] == 0x50) return "PDF";
        return "Diğer";
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) =>
        _scanCancellation?.Cancel();

    private void Quarantine_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Karantina yöneticisi henüz geliştirme aşamasında.",
            "Karantina", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Network_Click(object sender, RoutedEventArgs e) =>
        new NetworkConnectionsWindow { Owner = this }.Show();

    private void History_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show($"Bu oturumdaki {_activity.Count} etkinlik kaydı görüntüleniyor.",
            "Tarama geçmişi", MessageBoxButton.OK, MessageBoxImage.Information);

    private void AddActivity(string action, string details) =>
        _activity.Insert(0, new ActivityEntry(DateTime.Now.ToString("HH:mm:ss"), action, details));

    public sealed record ActivityEntry(string Time, string Action, string Details);
}
