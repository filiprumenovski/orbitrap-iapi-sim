namespace VirtualOrbitrap.Schema;

/// <summary>
/// Complete metadata for a single scan.
/// Mirrors ThermoRawFileReader.clsScanInfo.
/// </summary>
public class ScanInfo
{
    //=================================================================
    // CORE IDENTIFIERS
    //=================================================================

    /// <summary>
    /// Scan number (1-indexed).
    /// </summary>
    public int ScanNumber { get; set; }

    /// <summary>
    /// MS acquisition level.
    /// 1 = MS1 (survey scan)
    /// 2 = MS/MS (MS2)
    /// 3 = MS3
    /// </summary>
    public int MSLevel { get; set; }

    /// <summary>
    /// Event number within a scan segment.
    /// 1 = parent scan, 2 = first fragment, etc.
    /// </summary>
    public int EventNumber { get; set; }

    //=================================================================
    // TIMING
    //=================================================================

    /// <summary>
    /// Retention time in MINUTES.
    /// Start of scan acquisition relative to run start.
    /// </summary>
    public double RetentionTime { get; set; }

    /// <summary>
    /// Ion injection time in MILLISECONDS.
    /// Time ions were accumulated in the C-trap.
    /// Typical range: 1-500 ms.
    /// </summary>
    public double IonInjectionTime { get; set; }

    //=================================================================
    // SCAN STATISTICS
    //=================================================================

    /// <summary>
    /// Number of peaks (m/z-intensity pairs) in the scan.
    /// -1 if unknown.
    /// </summary>
    public int NumPeaks { get; set; } = -1;

    /// <summary>
    /// Total Ion Current - sum of all ion intensities.
    /// </summary>
    public double TotalIonCurrent { get; set; }

    /// <summary>
    /// m/z of the most abundant peak.
    /// </summary>
    public double BasePeakMZ { get; set; }

    /// <summary>
    /// Intensity of the most abundant peak.
    /// </summary>
    public double BasePeakIntensity { get; set; }

    /// <summary>
    /// Lowest observed m/z in the scan.
    /// </summary>
    public double LowMass { get; set; }

    /// <summary>
    /// Highest observed m/z in the scan.
    /// </summary>
    public double HighMass { get; set; }

    //=================================================================
    // FILTER STRING (Critical for downstream processing)
    //=================================================================

    /// <summary>
    /// Thermo filter string describing the scan.
    /// Examples:
    ///   "FTMS + p NSI Full ms [100.00-2000.00]"
    ///   "FTMS + p NSI d Full ms2 750.50@hcd35.00 [200.00-2000.00]"
    /// </summary>
    public string FilterText { get; set; } = string.Empty;

    //=================================================================
    // PARENT ION INFO (MS2+ scans)
    //=================================================================

    /// <summary>
    /// Precursor m/z for MS2+ scans.
    /// 0 for MS1 scans.
    /// </summary>
    public double ParentIonMZ { get; set; }

    /// <summary>
    /// Monoisotopic m/z as determined by instrument.
    /// From trailer extra "Monoisotopic M/Z:" event.
    /// Null if not available.
    /// </summary>
    public double? ParentIonMonoisotopicMZ { get; set; }

    /// <summary>
    /// Scan number of the parent (precursor) scan.
    /// 0 for MS1 scans.
    /// </summary>
    public int ParentScan { get; set; }

    /// <summary>
    /// Isolation window width in m/z units.
    /// Typical values: 1.0-2.0 for DDA, 10-50 for DIA.
    /// </summary>
    public double IsolationWindowWidthMZ { get; set; }

    /// <summary>
    /// List of parent ions with fragmentation details.
    /// Multiple entries for MSn where n &gt; 2.
    /// </summary>
    public List<ParentIonInfo> ParentIons { get; set; } = new();

    /// <summary>
    /// List of dependent (child) scan numbers.
    /// Empty for MS2+ scans.
    /// </summary>
    public List<int> DependentScans { get; set; } = new();

    //=================================================================
    // FRAGMENTATION INFO
    //=================================================================

    /// <summary>
    /// Activation/dissociation type.
    /// CID, HCD, ETD, EThcD, etc.
    /// </summary>
    public ActivationType ActivationType { get; set; } = ActivationType.Unknown;

    /// <summary>
    /// Collision mode string (lowercase).
    /// "cid", "hcd", "etd", "ethcd", "etcid"
    /// </summary>
    public string CollisionMode { get; set; } = string.Empty;

    //=================================================================
    // SCAN TYPE FLAGS
    //=================================================================

    /// <summary>
    /// True if Selected Ion Monitoring scan.
    /// </summary>
    public bool SIMScan { get; set; }

    /// <summary>
    /// Multiple Reaction Monitoring type.
    /// </summary>
    public MRMScanType MRMScanType { get; set; } = MRMScanType.NotMRM;

    /// <summary>
    /// True if zoom scan (narrow mass range).
    /// </summary>
    public bool ZoomScan { get; set; }

    /// <summary>
    /// True if Data-Independent Acquisition.
    /// Typically set when isolation width &gt;= 6.5 m/z.
    /// </summary>
    public bool IsDIA { get; set; }

    //=================================================================
    // INSTRUMENT FLAGS
    //=================================================================

    /// <summary>
    /// Ionization polarity.
    /// </summary>
    public IonMode IonMode { get; set; } = IonMode.Unknown;

    /// <summary>
    /// True if data is centroided (stick spectrum).
    /// False if profile (continuum) data.
    /// </summary>
    public bool IsCentroided { get; set; }

    /// <summary>
    /// True if acquired on high-resolution analyzer.
    /// (Orbitrap, FTMS, TOF, Astral)
    /// </summary>
    public bool IsHighResolution { get; set; }

    //=================================================================
    // SCAN EVENTS (Key-Value Metadata)
    //=================================================================

    /// <summary>
    /// Trailer extra / scan event key-value pairs.
    /// Common keys:
    ///   "Monoisotopic M/Z:"
    ///   "Charge State:"
    ///   "MS2 Isolation Width:"
    ///   "Ion Injection Time (ms):"
    ///   "AGC Target:"
    /// </summary>
    public List<KeyValuePair<string, string>> ScanEvents { get; set; } = new();

    /// <summary>
    /// Status log key-value pairs.
    /// </summary>
    public List<KeyValuePair<string, string>> StatusLog { get; set; } = new();

    //=================================================================
    // MRM INFO (for SRM/MRM scans)
    //=================================================================

    /// <summary>
    /// MRM configuration if applicable.
    /// </summary>
    public MRMInfo? MRMInfo { get; set; }

    //=================================================================
    // CONTROLLER PARAMETERS
    //=================================================================

    /// <summary>
    /// Number of channels (for multi-channel detectors).
    /// </summary>
    public int NumChannels { get; set; }

    /// <summary>
    /// True if uniform time sampling.
    /// </summary>
    public bool UniformTime { get; set; }

    /// <summary>
    /// Sampling frequency (Hz).
    /// </summary>
    public double Frequency { get; set; }

    //=================================================================
    // HELPER METHODS
    //=================================================================

    /// <summary>
    /// Try to get a scan event value by key name.
    /// </summary>
    public bool TryGetScanEvent(string eventName, out string eventValue, bool partialMatch = false)
    {
        foreach (var kvp in ScanEvents)
        {
            bool matches = partialMatch
                ? kvp.Key.StartsWith(eventName, StringComparison.OrdinalIgnoreCase)
                : kvp.Key.Equals(eventName, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                eventValue = kvp.Value;
                return true;
            }
        }
        eventValue = string.Empty;
        return false;
    }

    /// <summary>
    /// Store scan events from parallel string arrays.
    /// </summary>
    public void StoreScanEvents(string[] names, string[] values)
    {
        ScanEvents.Clear();
        for (int i = 0; i < names.Length && i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(names[i]))
            {
                ScanEvents.Add(new KeyValuePair<string, string>(names[i], values[i]?.Trim() ?? string.Empty));
            }
        }
    }
}
