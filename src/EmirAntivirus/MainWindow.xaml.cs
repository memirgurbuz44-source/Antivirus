using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using EmirAntivirus.Services;
using Microsoft.Win32;

namespace EmirAntivirus;

public partial class MainWindow : Window
{
    private readonly SignatureScanner _scanner = new();
    private readonly DefenderScanner _defender = new();
    private readonly DeepFileInspector _deepInspector = new();
    private readonly UpdateService _updates = new();
    private readonly ObservableCollection<ActivityEntry> _activity = new();
    private CancellationTokenSource? _scanCancellation;
    private string? _releaseUrl;

    public MainWindow()
    {
        InitializeComponent();
        ActivityList.ItemsSource = _activity;
        AddActivity("Başlatıldı", $"Sürüm {_updates.CurrentVersion}. Microsoft Defender entegrasyonu ve ek yerel kontroller hazır.");
        ProtectionStatus.Text = "ÇOK KATMANLI TARAMA HAZIR";
        Loaded += async (_, _) => await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await _updates.CheckAsync();
            if (update is null) return;
            _releaseUrl = update.ReleaseUrl;
            UpdateButton.Content = $"GÜNCELLEME VAR • {update.TagName}";
            UpdateButton.Visibility = Visibility.Visible;
            AddActivity("Güncelleme bulundu", $"{update.TagName} sürümü yayımlandı.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            AddActivity("Güncelleme", "Güncelleme sunucusuna ulaşılamadı; daha sonra tekrar deneyin.");
        }
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_releaseUrl))
            Process.Start(new ProcessStartInfo(_releaseUrl) { UseShellExecute = true });
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Taranacak klasörü seçin" };
        if (dialog.ShowDialog() == true)
            await RunScanAsync(new[] { dialog.FolderName }, true, "Özel klasör taraması");
    }

    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(user, "Downloads"), Path.Combine(user, "Documents"),
            Path.Combine(user, "AppData", "Local", "Temp") };
        await RunScanAsync(paths.Where(Directory.Exists), false, "Hızlı tarama");
    }

    private async void FullScan_Click(object sender, RoutedEventArgs e)
    {
        var roots = GetFixedDriveRoots();
        if (roots.Length == 0) { MessageBox.Show("Taranabilir sabit disk bulunamadı."); return; }
        await RunScanAsync(roots, false, "Tam tarama");
    }

    private async void DeepScan_Click(object sender, RoutedEventArgs e)
    {
        var roots = GetFixedDriveRoots();
        if (roots.Length == 0) { MessageBox.Show("Taranabilir sabit disk bulunamadı."); return; }
        var answer = MessageBox.Show("Derin tarama erişilebilir sabit disklerde dosyaların SHA-256 özetini, başlık/tür bilgisini ve boyutunu inceler. Uzun sürebilir. Dosyalar çalıştırılmaz, silinmez veya değiştirilmez. Devam edilsin mi?",
            "Derin tarama", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes) await RunScanAsync(roots, true, "Derin tarama");
    }

    private static string[] GetFixedDriveRoots() => DriveInfo.GetDrives()
        .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
        .Select(d => d.RootDirectory.FullName).ToArray();

    private async Task RunScanAsync(IEnumerable<string> roots, bool deep, string scanName)
    {
        if (_scanCancellation is not null) return;
        _scanCancellation = new CancellationTokenSource();
        var token = _scanCancellation.Token;
        CancelButton.IsEnabled = true;
        ScanProgress.IsIndeterminate = true;
        ScanProgress.Value = 0;
        UpdateCounters(0, 0, 0, 0);
        ProtectionStatus.Text = "TARAMA ÇALIŞIYOR";
        long checkedCount = 0, findings = 0, reviews = 0, skipped = 0;
        var rootList = roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var stack = new Stack<string>(rootList.Reverse());

        try
        {
            // Run the installed Microsoft Defender engine first; the local checks below are supplemental.
            foreach (var root in rootList)
            {
                token.ThrowIfCancellationRequested();
                StatusText.Text = $"Microsoft Defender tarıyor: {root}";
                var defenderResult = await _defender.ScanPathAsync(root, token);
                AddActivity(defenderResult.Available ? "Microsoft Defender" : "Defender kullanılamıyor",
                    $"{root} — {defenderResult.Message}");
                if (!defenderResult.Available || defenderResult.ExitCode is not 0)
                    reviews++;
                UpdateCounters(checkedCount, findings, reviews, skipped);
            }

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
                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) stack.Push(child);
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
                                var inspection = await _deepInspector.InspectAsync(file, token);
                                if (inspection.HeaderExtensionMismatch)
                                {
                                    reviews++;
                                    AddActivity("Elle kontrol gerekli", $"{inspection.Note} Tür: {inspection.FileType}; Boyut: {inspection.SizeBytes:N0} bayt; {file}");
                                }
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
                        catch (NotSupportedException) { skipped++; }

                        if (checkedCount % 20 == 0)
                        {
                            UpdateCounters(checkedCount, findings, reviews, skipped);
                            StatusText.Text = $"{scanName}: {checkedCount:N0} dosya kontrol edildi...";
                            await Task.Yield();
                        }
                    }
                }
                catch (UnauthorizedAccessException) { skipped++; }
                catch (IOException) { skipped++; }
            }

            ScanProgress.IsIndeterminate = false;
            ScanProgress.Value = 100;
            StatusText.Text = $"{scanName} tamamlandı. {checkedCount:N0} dosya, {findings:N0} imza eşleşmesi, {reviews:N0} inceleme uyarısı, {skipped:N0} erişilemeyen öğe.";
            ProtectionStatus.Text = "TARAMA TAMAMLANDI";
            AddActivity(scanName, StatusText.Text);
            if (findings == 0)
                MessageBox.Show("Yerel imza listesinde eşleşme bulunmadı. Microsoft Defender sonuçları Windows Güvenliği > Virüs ve tehdit koruması > Koruma geçmişi bölümünde kontrol edilebilir. Bu uygulama hiçbir taramayla cihazın kesin temiz olduğunu garanti etmez.",
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
            UpdateCounters(checkedCount, findings, reviews, skipped);
            CancelButton.IsEnabled = false;
            ScanProgress.IsIndeterminate = false;
            _scanCancellation.Dispose();
            _scanCancellation = null;
        }
    }

    private void UpdateCounters(long files, long matches, long reviews, long skipped)
    {
        FilesCountText.Text = files.ToString("N0");
        FindingsCountText.Text = matches.ToString("N0");
        ReviewCountText.Text = reviews.ToString("N0");
        SkippedCountText.Text = skipped.ToString("N0");
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCancellation?.Cancel();
    private void Network_Click(object sender, RoutedEventArgs e) => new NetworkConnectionsWindow { Owner = this }.Show();
    private void AddActivity(string action, string details) =>
        _activity.Insert(0, new ActivityEntry(DateTime.Now.ToString("HH:mm:ss"), action, details));
    public sealed record ActivityEntry(string Time, string Action, string Details);
}