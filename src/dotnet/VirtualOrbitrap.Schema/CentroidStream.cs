namespace VirtualOrbitrap.Schema;

/// <summary>
/// Represents centroid peak data from an Orbitrap scan.
/// All arrays are parallel (same index = same peak).
/// Mirrors ThermoFisher.CommonCore.Data.Business.CentroidStream
/// </summary>
public class CentroidStream
{
    //=================================================================
    // CORE PEAK DATA (Required for MVP)
    //=================================================================

    /// <summary>
    /// Mass-to-charge ratios for each centroid peak.
    /// Values are observed m/z (NOT monoisotopic mass).
    /// Typical range: 100-2000 m/z for proteomics.
    /// </summary>
    public double[] Masses { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Intensity values for each peak.
    /// Units: arbitrary (counts or normalized).
    /// Dynamic range typically 1e2 to 1e10.
    /// </summary>
    public double[] Intensities { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Number of centroid peaks in this scan.
    /// Must equal length of all parallel arrays.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Scan number this data belongs to.
    /// 1-indexed (first scan = 1).
    /// </summary>
    public int ScanNumber { get; set; }

    //=================================================================
    // ORBITRAP-SPECIFIC METADATA (Required for MVP)
    //=================================================================

    /// <summary>
    /// Mass resolution at each peak position.
    /// Orbitrap resolution formula: R(m) = R0 * sqrt(m0/m)
    /// Typical values: 15,000 - 500,000 depending on settings.
    /// R0 = resolution at reference mass m0 (usually 200 m/z).
    /// </summary>
    public double[]? Resolutions { get; set; }

    /// <summary>
    /// Noise floor estimate at each peak position.
    /// Used for signal-to-noise calculation: S/N = Intensity / Noise
    /// Typical values: 10-1000 (instrument dependent).
    /// </summary>
    public double[]? Noises { get; set; }

    /// <summary>
    /// Baseline intensity at each peak position.
    /// Represents chemical/electronic background.
    /// Typical values: 100-5000.
    /// </summary>
    public double[]? Baselines { get; set; }

    //=================================================================
    // CHARGE STATE DATA (v1.1 - set to 0 for MVP)
    //=================================================================

    /// <summary>
    /// Charge state for each peak.
    /// 0 = undetermined/unknown.
    /// Positive integers for assigned charges (1, 2, 3, ...).
    /// Determined from isotope spacing or deconvolution.
    /// </summary>
    public int[]? Charges { get; set; }

    //=================================================================
    // CALIBRATION DATA (Optional)
    //=================================================================

    /// <summary>
    /// Mass calibration polynomial coefficients.
    /// Used to convert raw frequency to m/z.
    /// Typically 2-5 coefficients.
    /// </summary>
    public double[]? Coefficients { get; set; }

    /// <summary>
    /// Number of calibration coefficients.
    /// </summary>
    public int CoefficientsCount { get; set; }

    //=================================================================
    // PEAK FLAGS (Optional)
    //=================================================================

    /// <summary>
    /// Flags for each peak (saturated, reference, etc.).
    /// Use PeakOptions enum values.
    /// </summary>
    public PeakOptions[]? Flags { get; set; }

    //=================================================================
    // COMPUTED PROPERTIES
    //=================================================================

    /// <summary>
    /// m/z of the most intense peak in the scan.
    /// </summary>
    public double BasePeakMass => GetBasePeakMass();

    /// <summary>
    /// Intensity of the most intense peak.
    /// </summary>
    public double BasePeakIntensity => GetBasePeakIntensity();

    /// <summary>
    /// Resolution at the base peak position.
    /// </summary>
    public double BasePeakResolution => GetBasePeakResolution();

    /// <summary>
    /// Noise at the base peak position.
    /// </summary>
    public double BasePeakNoise => GetBasePeakNoise();

    //=================================================================
    // HELPER METHODS
    //=================================================================

    private int _basePeakIndex = -1;

    private int FindBasePeakIndex()
    {
        if (_basePeakIndex >= 0) return _basePeakIndex;
        if (Intensities == null || Intensities.Length == 0) return -1;

        int maxIdx = 0;
        double maxInt = Intensities[0];
        for (int i = 1; i < Intensities.Length; i++)
        {
            if (Intensities[i] > maxInt)
            {
                maxInt = Intensities[i];
                maxIdx = i;
            }
        }
        _basePeakIndex = maxIdx;
        return maxIdx;
    }

    private double GetBasePeakMass() =>
        FindBasePeakIndex() >= 0 && Masses != null ? Masses[_basePeakIndex] : 0;

    private double GetBasePeakIntensity() =>
        FindBasePeakIndex() >= 0 && Intensities != null ? Intensities[_basePeakIndex] : 0;

    private double GetBasePeakResolution() =>
        FindBasePeakIndex() >= 0 && Resolutions != null ? Resolutions[_basePeakIndex] : 0;

    private double GetBasePeakNoise() =>
        FindBasePeakIndex() >= 0 && Noises != null ? Noises[_basePeakIndex] : 0;

    /// <summary>
    /// Calculate signal-to-noise ratio for a specific peak.
    /// </summary>
    public double GetSignalToNoise(int index)
    {
        if (Noises == null || index < 0 || index >= Length) return 0;
        return Noises[index] > 0 ? Intensities[index] / Noises[index] : 0;
    }

    /// <summary>
    /// Validate that all parallel arrays have consistent length.
    /// </summary>
    public bool Validate()
    {
        if (Masses == null || Intensities == null) return false;
        if (Masses.Length != Length || Intensities.Length != Length) return false;
        if (Resolutions != null && Resolutions.Length != Length) return false;
        if (Noises != null && Noises.Length != Length) return false;
        if (Baselines != null && Baselines.Length != Length) return false;
        if (Charges != null && Charges.Length != Length) return false;
        return true;
    }
}

/// <summary>
/// Peak option flags matching Thermo's PeakOptions enum.
/// </summary>
[Flags]
public enum PeakOptions
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>Peak is saturated.</summary>
    Saturated = 1,

    /// <summary>Peak is a reference ion.</summary>
    Reference = 2,

    /// <summary>Peak is marked as an exception.</summary>
    Exception = 4,

    /// <summary>Peak is fragmented.</summary>
    Fragmented = 8
}
