using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.IAPI;

/// <summary>
/// Event args for scan arrival events.
/// </summary>
public class ScanArrivedEventArgs : EventArgs
{
    /// <summary>
    /// Scan number.
    /// </summary>
    public int ScanNumber { get; set; }

    /// <summary>
    /// Retention time in minutes.
    /// </summary>
    public double RetentionTime { get; set; }

    /// <summary>
    /// MS level for the scan.
    /// </summary>
    public int MSLevel { get; set; }

    /// <summary>
    /// Centroid stream for the scan.
    /// </summary>
    public CentroidStream CentroidStream { get; set; } = null!;

    /// <summary>
    /// Scan metadata.
    /// </summary>
    public ScanInfo ScanInfo { get; set; } = null!;
}
