using System.Diagnostics;
using System.IO;

namespace EmirAntivirus.Services;

public sealed record DefenderScanResult(bool Available, int? ExitCode, string Message);

/// <summary>
/// Uses the installed Microsoft Defender command-line scanner. Does not execute scanned files.
/// </summary>
public sealed class DefenderScanner
{
    public async Task<DefenderScanResult> ScanPathAsync(string path, CancellationToken token)
    {
        var executable = FindMpCmdRun();
        if (executable is null)
            return new DefenderScanResult(false, null,
                "Microsoft Defender komut satırı tarayıcısı bulunamadı. Yerleşik tarama yapılamadı.");

        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-Scan");
        start.ArgumentList.Add("-ScanType");
        start.ArgumentList.Add("3");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(path);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                return new DefenderScanResult(false, null, "Microsoft Defender taraması başlatılamadı.");

            using var registration = token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            });

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(token);
            var code = process.ExitCode;
            var details = (await stdout) + " " + (await stderr);
            var message = code == 0
                ? "Microsoft Defender taraması tamamlandı."
                : $"Microsoft Defender taraması çıkış kodu: {code}. {details.Trim()}";
            return new DefenderScanResult(true, code, message);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new DefenderScanResult(true, null, $"Microsoft Defender taraması başlatılamadı: {ex.Message}");
        }
    }

    private static string? FindMpCmdRun()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var platformRoot = Path.Combine(programData, "Microsoft", "Windows Defender", "Platform");
        if (Directory.Exists(platformRoot))
        {
            var newest = Directory.GetDirectories(platformRoot)
                .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var folder in newest)
            {
                var candidate = Path.Combine(folder, "MpCmdRun.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var fallback = Path.Combine(programFiles, "Windows Defender", "MpCmdRun.exe");
        return File.Exists(fallback) ? fallback : null;
    }
}