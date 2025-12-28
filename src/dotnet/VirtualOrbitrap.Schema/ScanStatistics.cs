namespace VirtualOrbitrap.Schema;

/// <summary>
/// Scan-level statistics.
/// Lighter weight than full ScanInfo for quick access.
/// </summary>
public class ScanStatistics
{
    /// <summary>
    /// Scan number (1-indexed).
    /// </summary>
    public int ScanNumber { get; set; }

    /// <summary>
    /// Retention time in minutes.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// Total Ion Current.
    /// </summary>
    public double TIC { get; set; }

    /// <summary>
    /// Base peak intensity.
    /// </summary>
    public double BasePeakIntensity { get; set; }

    /// <summary>
    /// Base peak m/z.
    /// </summary>
    public double BasePeakMass { get; set; }

    /// <summary>
    /// Lowest m/z in scan.
    /// </summary>
    public double LowMass { get; set; }

    /// <summary>
    /// Highest m/z in scan.
    /// </summary>
    public double HighMass { get; set; }

    /// <summary>
    /// Packet type indicator (internal format).
    /// </summary>
    public int PacketType { get; set; }

    /// <summary>
    /// True if centroid data.
    /// </summary>
    public bool IsCentroidScan { get; set; }

    /// <summary>
    /// True if separate centroid stream exists (FTMS).
    /// </summary>
    public bool HasCentroidStream { get; set; }

    /// <summary>
    /// Number of channels.
    /// </summary>
    public int NumberOfChannels { get; set; }

    /// <summary>
    /// Uniform time sampling flag.
    /// </summary>
    public bool UniformTime { get; set; }

    /// <summary>
    /// Sampling frequency.
    /// </summary>
    public double Frequency { get; set; }
}
