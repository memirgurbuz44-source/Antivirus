using System.Diagnostics;
using System.Net;

namespace EmirAntivirus.Services;

public sealed record NetworkConnectionInfo(
    int ProcessId, string ProcessName, string LocalEndpoint,
    string RemoteEndpoint, string State, bool IsPublicDestination);

/// <summary>
/// Reads the operating system's current TCP connection table using netstat.
/// It does not intercept traffic, inspect encrypted content, or contact remote services.
/// </summary>
public static class NetworkConnectionReader
{
    public static IReadOnlyList<NetworkConnectionInfo> ReadActiveTcpConnections()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Bu özellik Windows içindir.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-ano -p tcp",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        var results = new List<NetworkConnectionInfo>();
        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 || !parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!int.TryParse(parts[^1], out var pid))
                continue;

            var local = parts[1];
            var remote = parts[2];
            var state = parts[3];
            string name;
            try { name = Process.GetProcessById(pid).ProcessName; }
            catch { name = "Erişilemedi"; }

            var address = ExtractAddress(remote);
            var isPublic = IPAddress.TryParse(address, out var ip) &&
                           !IPAddress.IsLoopback(ip) &&
                           !IsPrivateOrLocal(ip);

            results.Add(new NetworkConnectionInfo(pid, name, local, remote, state, isPublic));
        }

        return results
            .GroupBy(x => (x.ProcessId, x.LocalEndpoint, x.RemoteEndpoint, x.State))
            .Select(g => g.First())
            .OrderByDescending(x => x.IsPublicDestination)
            .ThenBy(x => x.ProcessName)
            .ToArray();
    }

    private static string ExtractAddress(string endpoint)
    {
        if (endpoint.StartsWith("[", StringComparison.Ordinal) &&
            endpoint.IndexOf(']') is var close && close > 0)
            return endpoint[1..close];

        var colon = endpoint.LastIndexOf(':');
        return colon > 0 ? endpoint[..colon] : endpoint;
    }

    private static bool IsPrivateOrLocal(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;

        var b = ip.GetAddressBytes();
        return b[0] == 10 || b[0] == 127 ||
               (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
               (b[0] == 192 && b[1] == 168) ||
               (b[0] == 169 && b[1] == 254);
    }
}
