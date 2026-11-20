using System.IO;

namespace EmirAntivirus.Services;

/// <summary>
/// Observes file-system events. The host decides when/how to scan changed files.
/// </summary>
public sealed class RealtimeMonitor : IDisposable
{
    private readonly FileSystemWatcher _watcher;

    public event EventHandler<string>? FileChanged;

    public RealtimeMonitor(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);

        _watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                           NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = false
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += (_, e) => Raise(e.FullPath);
    }

    public void Start() => _watcher.EnableRaisingEvents = true;
    public void Stop() => _watcher.EnableRaisingEvents = false;

    private void OnChanged(object sender, FileSystemEventArgs e) => Raise(e.FullPath);
    private void Raise(string path) => FileChanged?.Invoke(this, path);

    public void Dispose() => _watcher.Dispose();
}
