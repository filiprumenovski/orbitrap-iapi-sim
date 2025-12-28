namespace VirtualOrbitrap.Parsers.Dto;

/// <summary>
/// Represents a complete parsed mzML file with all scans and metadata.
/// </summary>
public sealed class ParsedRawFile
{
    /// <summary>
    /// Original source file path.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// File name without path.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Creation date of the original raw file (if available).
    /// </summary>
    public DateTime? CreationDate { get; init; }

    /// <summary>
    /// Instrument model name.
    /// </summary>
    public string InstrumentModel { get; init; } = string.Empty;

    /// <summary>
    /// Instrument serial number.
    /// </summary>
    public string InstrumentSerialNumber { get; init; } = string.Empty;

    /// <summary>
    /// Software version used for acquisition or conversion.
    /// </summary>
    public string SoftwareVersion { get; init; } = string.Empty;

    /// <summary>
    /// Total number of scans in the file.
    /// </summary>
    public int TotalScans => Scans.Count;

    /// <summary>
    /// First scan number.
    /// </summary>
    public int FirstScanNumber => Scans.Count > 0 ? Scans[0].ScanNumber : 0;

    /// <summary>
    /// Last scan number.
    /// </summary>
    public int LastScanNumber => Scans.Count > 0 ? Scans[^1].ScanNumber : 0;

    /// <summary>
    /// Start retention time in minutes.
    /// </summary>
    public double StartTime => Scans.Count > 0 ? Scans[0].RetentionTimeMinutes : 0;

    /// <summary>
    /// End retention time in minutes.
    /// </summary>
    public double EndTime => Scans.Count > 0 ? Scans[^1].RetentionTimeMinutes : 0;

    /// <summary>
    /// All parsed scans.
    /// </summary>
    public IReadOnlyList<ParsedScan> Scans { get; init; } = Array.Empty<ParsedScan>();

    /// <summary>
    /// Get MS1 scans only.
    /// </summary>
    public IEnumerable<ParsedScan> Ms1Scans => Scans.Where(s => s.MsLevel == 1);

    /// <summary>
    /// Get MS2+ scans only.
    /// </summary>
    public IEnumerable<ParsedScan> MsnScans => Scans.Where(s => s.MsLevel > 1);
}
