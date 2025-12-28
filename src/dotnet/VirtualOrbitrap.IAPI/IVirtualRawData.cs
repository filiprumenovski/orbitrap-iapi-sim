using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.IAPI;

/// <summary>
/// Interface matching key methods of Thermo's IRawDataPlus.
/// Implement this to provide IAPI-compatible data access.
/// </summary>
public interface IVirtualRawData : IDisposable
{
    //=================================================================
    // FILE METADATA
    //=================================================================

    /// <summary>
    /// File-level metadata.
    /// </summary>
    RawFileInfo FileInfo { get; }

    /// <summary>
    /// First scan number in the file.
    /// </summary>
    int ScanStart { get; }

    /// <summary>
    /// Last scan number in the file.
    /// </summary>
    int ScanEnd { get; }

    /// <summary>
    /// Total number of scans.
    /// </summary>
    int NumScans { get; }

    //=================================================================
    // SCAN ACCESS
    //=================================================================

    /// <summary>
    /// Get centroid stream for a scan.
    /// Primary method for accessing Orbitrap peak data.
    /// </summary>
    CentroidStream GetCentroidStream(int scanNumber);

    /// <summary>
    /// Get scan metadata.
    /// </summary>
    ScanInfo GetScanInfo(int scanNumber);

    /// <summary>
    /// Get scan statistics (lighter weight than full ScanInfo).
    /// </summary>
    ScanStatistics GetScanStatistics(int scanNumber);

    /// <summary>
    /// Get retention time for a scan.
    /// </summary>
    double GetRetentionTime(int scanNumber);

    /// <summary>
    /// Get MS level for a scan.
    /// </summary>
    int GetMSLevel(int scanNumber);

    /// <summary>
    /// Get filter text for a scan.
    /// </summary>
    string GetFilterText(int scanNumber);

    //=================================================================
    // ALTERNATIVE DATA ACCESS
    //=================================================================

    /// <summary>
    /// Get label data (alternative to CentroidStream for some uses).
    /// </summary>
    FTLabelInfo[] GetScanLabelData(int scanNumber);

    /// <summary>
    /// Get mass precision data.
    /// </summary>
    MassPrecisionInfo[] GetScanPrecisionData(int scanNumber);

    //=================================================================
    // EVENTS
    //=================================================================

    /// <summary>
    /// Event raised when a new scan arrives.
    /// Use for streaming/real-time simulation.
    /// </summary>
    event EventHandler<ScanArrivedEventArgs> ScanArrived;
}
