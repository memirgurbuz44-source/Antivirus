using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EmirAntivirus.Models;

namespace EmirAntivirus.Services;

/// <summary>
/// Checks files against locally trusted SHA-256 signatures.
/// It never executes the file being inspected.
/// </summary>
public sealed class SignatureScanner
{
    private readonly IReadOnlyDictionary<string, string> _signatures;

    public SignatureScanner(IReadOnlyDictionary<string, string>? signatures = null)
    {
        _signatures = signatures ?? new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ScanFinding?> ScanFileAsync(
        string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return null;

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = Convert.ToHexString(
            await SHA256.HashDataAsync(stream, cancellationToken));

        return _signatures.TryGetValue(hash, out var name)
            ? new ScanFinding(path, hash, name, DateTime.UtcNow,
                "Dosya yerel SHA-256 imza listesinde bulundu.")
            : null;
    }
}