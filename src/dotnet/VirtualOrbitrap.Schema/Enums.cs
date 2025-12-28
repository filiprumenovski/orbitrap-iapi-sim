namespace VirtualOrbitrap.Schema;

/// <summary>
/// Activation/dissociation types.
/// Mirrors ThermoFisher.CommonCore.Data.FilterEnums.ActivationType
/// </summary>
public enum ActivationType
{
    /// <summary>Unknown activation type.</summary>
    Unknown = -1,

    /// <summary>Collision-Induced Dissociation.</summary>
    CID = 0,

    /// <summary>Multi Photon Dissociation.</summary>
    MPD = 1,

    /// <summary>Electron Capture Dissociation.</summary>
    ECD = 2,

    /// <summary>Pulsed Q Dissociation.</summary>
    PQD = 3,

    /// <summary>Electron Transfer Dissociation.</summary>
    ETD = 4,

    /// <summary>Higher-energy Collisional Dissociation.</summary>
    HCD = 5,

    /// <summary>Any activation type.</summary>
    AnyType = 6,

    /// <summary>Supplemental Activation.</summary>
    SA = 7,

    /// <summary>Proton Transfer Reaction.</summary>
    PTR = 8,

    /// <summary>Negative ETD.</summary>
    NETD = 9,

    /// <summary>Negative PTR.</summary>
    NPTR = 10,

    /// <summary>Ultraviolet Photodissociation.</summary>
    UVPD = 11
}

/// <summary>
/// MRM/SRM scan types.
/// </summary>
public enum MRMScanType
{
    /// <summary>Not an MRM scan.</summary>
    NotMRM = 0,

    /// <summary>Multiple SIM ranges.</summary>
    MRMQMS = 1,

    /// <summary>Selected Reaction Monitoring.</summary>
    SRM = 2,

    /// <summary>Full Neutral Loss.</summary>
    FullNL = 3,

    /// <summary>Selected Ion Monitoring.</summary>
    SIM = 4
}

/// <summary>
/// Ionization polarity.
/// </summary>
public enum IonMode
{
    /// <summary>Unknown polarity.</summary>
    Unknown = 0,

    /// <summary>Positive mode.</summary>
    Positive = 1,

    /// <summary>Negative mode.</summary>
    Negative = 2
}

/// <summary>
/// Mass analyzer types.
/// </summary>
public enum MassAnalyzer
{
    /// <summary>Any analyzer.</summary>
    Any,

    /// <summary>Ion Trap.</summary>
    ITMS,

    /// <summary>Triple Quad.</summary>
    TQMS,

    /// <summary>Single Quad.</summary>
    SQMS,

    /// <summary>Time of Flight.</summary>
    TOFMS,

    /// <summary>Orbitrap / FT-ICR.</summary>
    FTMS,

    /// <summary>Magnetic Sector.</summary>
    Sector,

    /// <summary>Astral.</summary>
    ASTMS
}

/// <summary>
/// MS order (scan power).
/// </summary>
public enum MSOrder
{
    /// <summary>Any order.</summary>
    Any,

    /// <summary>MS1.</summary>
    Ms,

    /// <summary>MS/MS.</summary>
    Ms2,

    /// <summary>MS3.</summary>
    Ms3,

    /// <summary>MS4.</summary>
    Ms4,

    /// <summary>MS5.</summary>
    Ms5,

    /// <summary>MS6.</summary>
    Ms6,

    /// <summary>MS7.</summary>
    Ms7,

    /// <summary>MS8.</summary>
    Ms8,

    /// <summary>MS9.</summary>
    Ms9,

    /// <summary>MS10.</summary>
    Ms10,

    /// <summary>Parent scan.</summary>
    ParentScan,

    /// <summary>Zoom scan.</summary>
    ZoomScan
}

/// <summary>
/// Device types in raw files.
/// </summary>
public enum Device
{
    /// <summary>No device.</summary>
    None,

    /// <summary>Mass Spectrometer.</summary>
    MS,

    /// <summary>Analog MS device.</summary>
    MSAnalog,

    /// <summary>Analog device (LC pumps, etc.).</summary>
    Analog,

    /// <summary>UV detector.</summary>
    UV,

    /// <summary>Photo Diode Array.</summary>
    PDA,

    /// <summary>Other device.</summary>
    Other
}

/// <summary>
/// Data units for non-MS devices.
/// </summary>
public enum DataUnits
{
    /// <summary>Counts.</summary>
    None,

    /// <summary>Absorbance units.</summary>
    AbsorbanceUnits,

    /// <summary>Milli-absorbance units.</summary>
    MilliAbsorbanceUnits,

    /// <summary>Micro-absorbance units.</summary>
    MicroAbsorbanceUnits,

    /// <summary>Volts.</summary>
    Volts,

    /// <summary>Millivolts.</summary>
    MilliVolts,

    /// <summary>Microvolts.</summary>
    MicroVolts
}
