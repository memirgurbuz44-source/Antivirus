namespace EmirAntivirus.Models;

public sealed record ScanFinding(
    string FilePath,
    string Sha256,
    string DetectionName,
    DateTime DetectedAt,
    string Description);
