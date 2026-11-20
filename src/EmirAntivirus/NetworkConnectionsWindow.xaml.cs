using System.Windows;
using System.Windows.Threading;
using EmirAntivirus.Services;

namespace EmirAntivirus;

public partial class NetworkConnectionsWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };

    public NetworkConnectionsWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => RefreshConnections();
        Loaded += (_, _) =>
        {
            RefreshConnections();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshConnections();

    private void RefreshConnections()
    {
        try
        {
            var rows = NetworkConnectionReader.ReadActiveTcpConnections()
                .Select(x => new ConnectionRow(
                    x.ProcessName, x.ProcessId, x.LocalEndpoint, x.RemoteEndpoint,
                    x.State, x.IsPublicDestination ? "Genel internet" : "Yerel / özel / çözümlenemedi"))
                .ToArray();

            ConnectionsGrid.ItemsSource = rows;
            var publicCount = rows.Count(x => x.DestinationType == "Genel internet");
            SummaryText.Text = $"{rows.Length} TCP bağlantısı • {publicCount} genel internet hedefi • 3 saniyede bir yenilenir";
        }
        catch (Exception ex)
        {
            SummaryText.Text = "Bağlantılar okunamadı: " + ex.Message;
        }
    }

    private sealed record ConnectionRow(
        string ProcessName, int ProcessId, string LocalEndpoint,
        string RemoteEndpoint, string State, string DestinationType);
}
