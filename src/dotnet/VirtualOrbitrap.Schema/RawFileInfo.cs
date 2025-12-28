namespace VirtualOrbitrap.Schema;

/// <summary>
/// File-level metadata for the virtual raw file.
/// </summary>
public class RawFileInfo
{
    //=================================================================
    // SAMPLE INFO
    //=================================================================

    /// <summary>Acquisition date string.</summary>
    public string AcquisitionDate { get; set; } = string.Empty;

    /// <summary>Acquisition filename.</summary>
    public string AcquisitionFilename { get; set; } = string.Empty;

    /// <summary>First comment field.</summary>
    public string Comment1 { get; set; } = string.Empty;

    /// <summary>Second comment field.</summary>
    public string Comment2 { get; set; } = string.Empty;

    /// <summary>Sample name.</summary>
    public string SampleName { get; set; } = string.Empty;

    /// <summary>Sample comment.</summary>
    public string SampleComment { get; set; } = string.Empty;

    //=================================================================
    // FILE CREATION
    //=================================================================

    /// <summary>Date when the file was created.</summary>
    public DateTime CreationDate { get; set; } = DateTime.Now;

    /// <summary>Creator identifier.</summary>
    public string CreatorID { get; set; } = Environment.UserName;

    /// <summary>RAW format version number.</summary>
    public int VersionNumber { get; set; } = 66;

    //=================================================================
    // INSTRUMENT INFO
    //=================================================================

    /// <summary>
    /// Instrument name (e.g., "Orbitrap Exploris 480")
    /// </summary>
    public string InstName { get; set; } = "Virtual Orbitrap";

    /// <summary>
    /// Instrument model (e.g., "Orbitrap Exploris 480")
    /// </summary>
    public string InstModel { get; set; } = "Virtual Orbitrap Simulator";

    /// <summary>
    /// Serial number.
    /// </summary>
    public string InstSerialNumber { get; set; } = "SIM-001";

    /// <summary>
    /// Hardware version string.
    /// </summary>
    public string InstHardwareVersion { get; set; } = "1.0";

    /// <summary>
    /// Software version string.
    /// </summary>
    public string InstSoftwareVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Instrument flags (TIM, NLM, PIM, DDZMap).
    /// </summary>
    public string InstFlags { get; set; } = string.Empty;

    /// <summary>
    /// Additional instrument description.
    /// </summary>
    public string InstrumentDescription { get; set; } = "Virtual Orbitrap IAPI Simulator";

    //=================================================================
    // DEVICE TRACKING
    //=================================================================

    /// <summary>
    /// Devices present in the file.
    /// Key = Device type, Value = count of that device type.
    /// </summary>
    public Dictionary<Device, int> Devices { get; set; } = new()
    {
        { Device.MS, 1 }
    };

    //=================================================================
    // METHODS
    //=================================================================

    /// <summary>
    /// Instrument methods (acquisition methods).
    /// </summary>
    public List<string> InstMethods { get; set; } = new();

    /// <summary>
    /// Tune methods and their settings.
    /// </summary>
    public List<TuneMethod> TuneMethods { get; set; } = new();

    //=================================================================
    // SCAN RANGE
    //=================================================================

    /// <summary>
    /// First scan number in the file.
    /// </summary>
    public int ScanStart { get; set; } = 1;

    /// <summary>
    /// Last scan number in the file.
    /// </summary>
    public int ScanEnd { get; set; }

    /// <summary>
    /// Retention time of first scan (minutes).
    /// </summary>
    public double FirstScanTimeMinutes { get; set; }

    /// <summary>
    /// Retention time of last scan (minutes).
    /// </summary>
    public double LastScanTimeMinutes { get; set; }

    /// <summary>
    /// Mass resolution setting.
    /// </summary>
    public double MassResolution { get; set; } = 120000;

    //=================================================================
    // STATUS FLAGS
    //=================================================================

    /// <summary>No MS device present.</summary>
    public bool HasNoMSDevice { get; set; }

    /// <summary>Non-MS device data is present.</summary>
    public bool HasNonMSDataDevice { get; set; }

    /// <summary>File is corrupt.</summary>
    public bool CorruptFile { get; set; }
}

/// <summary>
/// Tune method settings container.
/// </summary>
public class TuneMethod
{
    /// <summary>
    /// Tune method settings.
    /// </summary>
    public List<TuneMethodSetting> Settings { get; set; } = new();
}

/// <summary>
/// Individual tune method setting.
/// </summary>
public struct TuneMethodSetting
{
    /// <summary>Setting category.</summary>
    public string Category { get; set; }

    /// <summary>Setting name.</summary>
    public string Name { get; set; }

    /// <summary>Setting value.</summary>
    public string Value { get; set; }
}
