namespace Orbitrap.Abstractions;

/// <summary>
/// Unified scan interface: real Orbitrap and mock both implement this.
/// Downstream code depends ONLY on this, not on specific implementations.
/// This is the "thin waist" - the contract that both real and mock converge to.
/// </summary>
public interface IOrbitrapScan
{
    #region Identification

    /// <summary>
    /// Sequential scan number within the acquisition.
    /// </summary>
    int ScanNumber { get; }

    /// <summary>
    /// MS order: 1 = MS1 (survey scan), 2 = MS2 (fragmentation scan).
    /// </summary>
    int MsOrder { get; }

    /// <summary>
    /// Retention time in minutes from the start of the chromatographic run.
    /// </summary>
    double RetentionTime { get; }

    #endregion

    #region Spectrum Data

    /// <summary>
    /// Mass-to-charge (m/z) values of the spectrum peaks.
    /// Uses ReadOnlyMemory for zero-copy slicing and ArrayPool compatibility.
    /// </summary>
    ReadOnlyMemory<double> MzValues { get; }

    /// <summary>
    /// Intensity values corresponding to each m/z peak.
    /// Uses ReadOnlyMemory for zero-copy slicing and ArrayPool compatibility.
    /// </summary>
    ReadOnlyMemory<double> IntensityValues { get; }

    /// <summary>
    /// Number of peaks in the spectrum.
    /// </summary>
    int PeakCount { get; }

    #endregion

    #region Aggregates

    /// <summary>
    /// m/z value of the most intense peak (base peak).
    /// </summary>
    double BasePeakMz { get; }

    /// <summary>
    /// Intensity of the most intense peak (base peak).
    /// </summary>
    double BasePeakIntensity { get; }

    /// <summary>
    /// Sum of all ion intensities in the spectrum.
    /// </summary>
    double TotalIonCurrent { get; }

    #endregion

    #region Precursor Information (MS2+ only)

    /// <summary>
    /// Precursor mass for MS2+ scans. Null for MS1 scans.
    /// </summary>
    double? PrecursorMass { get; }

    /// <summary>
    /// Charge state of the precursor ion. Null for MS1 scans.
    /// </summary>
    int? PrecursorCharge { get; }

    /// <summary>
    /// Intensity of the precursor ion in the MS1 scan. Null for MS1 scans.
    /// </summary>
    double? PrecursorIntensity { get; }

    /// <summary>
    /// Isolation window width in m/z units. Null for MS1 scans.
    /// </summary>
    double? IsolationWidth { get; }

    /// <summary>
    /// Collision energy used for fragmentation (eV). Null for MS1 scans.
    /// </summary>
    double? CollisionEnergy { get; }

    /// <summary>
    /// Fragmentation method (e.g., CID, HCD, ETD). Null for MS1 scans.
    /// </summary>
    FragmentationType? FragmentationType { get; }

    #endregion

    #region Analyzer Metadata

    /// <summary>
    /// Mass analyzer type (e.g., "Orbitrap", "Ion Trap").
    /// </summary>
    string Analyzer { get; }

    /// <summary>
    /// Resolution setting at m/z 200.
    /// </summary>
    double ResolutionAtMz200 { get; }

    /// <summary>
    /// Mass accuracy in parts per million (ppm).
    /// </summary>
    double MassAccuracyPpm { get; }

    /// <summary>
    /// Polarity of the scan.
    /// </summary>
    Polarity Polarity { get; }

    #endregion

    #region Extended Metadata

    /// <summary>
    /// Additional metadata key-value pairs (trailer extra values in Thermo terminology).
    /// </summary>
    IReadOnlyDictionary<string, string> TrailerExtra { get; }

    #endregion

    #region Immutability

    /// <summary>
    /// Creates an immutable, thread-safe snapshot of this scan.
    /// Use when passing scans across threads or storing for later processing.
    /// </summary>
    FrozenOrbitrapScan ToFrozen();

    #endregion
}

/// <summary>
/// Fragmentation method used for MS2+ scans.
/// </summary>
public enum FragmentationType
{
    /// <summary>Unknown or unspecified fragmentation type.</summary>
    Unknown = 0,

    /// <summary>Collision-Induced Dissociation.</summary>
    CID,

    /// <summary>Higher-energy Collisional Dissociation.</summary>
    HCD,

    /// <summary>Electron Transfer Dissociation.</summary>
    ETD,

    /// <summary>Electron-Transfer/Higher-Energy Collision Dissociation.</summary>
    EThcD,

    /// <summary>Ultraviolet Photodissociation.</summary>
    UVPD
}

/// <summary>
/// Ion polarity mode.
/// </summary>
public enum Polarity
{
    /// <summary>Unknown polarity.</summary>
    Unknown = 0,

    /// <summary>Positive ion mode.</summary>
    Positive,

    /// <summary>Negative ion mode.</summary>
    Negative
}

/// <summary>
/// Event args carrying an IOrbitrapScan, used by both real and mock instruments.
/// </summary>
public sealed class OrbitrapScanEventArgs : EventArgs
{
    public OrbitrapScanEventArgs(IOrbitrapScan scan)
    {
        Scan = scan ?? throw new ArgumentNullException(nameof(scan));
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// The scan data.
    /// </summary>
    public IOrbitrapScan Scan { get; }

    /// <summary>
    /// UTC timestamp when the event was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}
