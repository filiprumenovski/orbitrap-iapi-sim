namespace VirtualOrbitrap.Schema;

/// <summary>
/// Mass precision/accuracy information.
/// </summary>
public struct MassPrecisionInfo
{
    /// <summary>
    /// m/z value.
    /// </summary>
    public double Mass { get; set; }

    /// <summary>
    /// Peak intensity.
    /// </summary>
    public double Intensity { get; set; }

    /// <summary>
    /// Mass accuracy in millimass units.
    /// </summary>
    public double AccuracyMMU { get; set; }

    /// <summary>
    /// Mass accuracy in parts per million.
    /// </summary>
    public double AccuracyPPM { get; set; }

    /// <summary>
    /// Mass resolution.
    /// </summary>
    public double Resolution { get; set; }
}
