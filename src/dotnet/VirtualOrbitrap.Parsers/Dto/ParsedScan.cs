namespace VirtualOrbitrap.Parsers.Dto;

/// <summary>
/// Lightweight data-transfer object representing a parsed scan from mzML.
/// Contains raw data extracted from mzML before enrichment/conversion.
/// </summary>
public sealed class ParsedScan
{
    /// <summary>
    /// The original scan index from mzML (0-indexed).
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Scan number (1-indexed) derived from native ID or index.
    /// </summary>
    public int ScanNumber { get; init; }

    /// <summary>
    /// MS level (1=MS1, 2=MS/MS, etc.).
    /// </summary>
    public int MsLevel { get; init; }

    /// <summary>
    /// Retention time in minutes.
    /// </summary>
    public double RetentionTimeMinutes { get; init; }

    /// <summary>
    /// m/z values for each peak.
    /// </summary>
    public double[] Mzs { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Intensity values for each peak.
    /// </summary>
    public double[] Intensities { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Number of peaks in this scan.
    /// </summary>
    public int PeakCount => Mzs.Length;

    /// <summary>
    /// True if spectrum is centroided, false if profile.
    /// </summary>
    public bool IsCentroid { get; init; }

    /// <summary>
    /// Ion polarity (+1 for positive, -1 for negative, 0 unknown).
    /// </summary>
    public int Polarity { get; init; }

    /// <summary>
    /// Total ion current (sum of intensities).
    /// </summary>
    public double TotalIonCurrent { get; init; }

    /// <summary>
    /// Base peak m/z (m/z of the most intense peak).
    /// </summary>
    public double BasePeakMz { get; init; }

    /// <summary>
    /// Base peak intensity.
    /// </summary>
    public double BasePeakIntensity { get; init; }

    /// <summary>
    /// Lowest m/z in the scan range.
    /// </summary>
    public double LowMz { get; init; }

    /// <summary>
    /// Highest m/z in the scan range.
    /// </summary>
    public double HighMz { get; init; }

    /// <summary>
    /// Precursor information (null for MS1 scans).
    /// </summary>
    public PrecursorInfo? Precursor { get; init; }

    /// <summary>
    /// Ion injection time in milliseconds (if available).
    /// </summary>
    public double? InjectionTimeMs { get; init; }
}
