using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmirAntivirus.Services;

public sealed record DeepFileInspection(string FileType, long SizeBytes, bool HeaderExtensionMismatch, string Note);

public sealed class DeepFileInspector
{
    public async Task<DeepFileInspection> InspectAsync(string path, CancellationToken token)
    {
        var info = new FileInfo(path);
        var header = new byte[4096];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, header.Length,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var read = await stream.ReadAsync(header.AsMemory(), token);
        var type = DetectType(header, read);
        var ext = info.Extension.ToLowerInvariant();
        var allowedExecutableExtensions = new[] { ".exe", ".dll", ".sys", ".ocx", ".scr", ".cpl" };
        var mismatch = type == "Windows çalıştırılabilir dosyası" &&
            !allowedExecutableExtensions.Contains(ext);
        var note = mismatch
            ? "Başlık Windows çalıştırılabilir dosyasına benziyor ancak uzantısı farklı; elle kontrol edilmeli."
            : "Dosya başlığı ve temel metadata incelendi; dosya çalıştırılmadı.";
        return new DeepFileInspection(type, info.Length, mismatch, note);
    }

    private static string DetectType(byte[] b, int n)
    {
        if (n >= 2 && b[0] == 0x4D && b[1] == 0x5A) return "Windows çalıştırılabilir dosyası";
        if (n >= 4 && b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04) return "ZIP/arşiv";
        if (n >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return "PDF belgesi";
        if (n >= 8 && b.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "PNG görsel";
        if (n >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "JPEG görsel";
        if (n >= 4 && b[0] == 0x7F && b[1] == 0x45 && b[2] == 0x4C && b[3] == 0x46) return "ELF çalıştırılabilir dosyası";
        if (n >= 4 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0) return "OLE belge/kapsayıcı";
        return "Bilinmeyen/metin/diğer";
    }
}