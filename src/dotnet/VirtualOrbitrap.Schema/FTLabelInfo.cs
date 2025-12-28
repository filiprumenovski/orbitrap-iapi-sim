namespace VirtualOrbitrap.Schema;

/// <summary>
/// High-resolution peak label data.
/// Alternative representation to CentroidStream for some APIs.
/// </summary>
public struct FTLabelInfo
{
    /// <summary>
    /// Observed m/z (NOT monoisotopic).
    /// </summary>
    public double Mass { get; set; }

    /// <summary>
    /// Peak intensity.
    /// </summary>
    public double Intensity { get; set; }

    /// <summary>
    /// Mass resolution at this peak.
    /// </summary>
    public float Resolution { get; set; }

    /// <summary>
    /// Baseline intensity.
    /// </summary>
    public float Baseline { get; set; }

    /// <summary>
    /// Noise floor.
    /// </summary>
    public float Noise { get; set; }

    /// <summary>
    /// Charge state (0 if undetermined).
    /// </summary>
    public int Charge { get; set; }

    /// <summary>
    /// Signal-to-noise ratio.
    /// </summary>
    public double SignalToNoise => Noise > 0 ? Intensity / Noise : 0;
}
