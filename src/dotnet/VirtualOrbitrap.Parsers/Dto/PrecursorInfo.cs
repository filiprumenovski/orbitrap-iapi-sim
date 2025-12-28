namespace VirtualOrbitrap.Parsers.Dto;

/// <summary>
/// Precursor ion information for MS2+ scans.
/// </summary>
public sealed class PrecursorInfo
{
    /// <summary>
    /// Selected precursor m/z.
    /// </summary>
    public double SelectedMz { get; init; }

    /// <summary>
    /// Monoisotopic m/z (if determined).
    /// </summary>
    public double? MonoisotopicMz { get; init; }

    /// <summary>
    /// Isolation window target m/z (center of isolation window).
    /// </summary>
    public double IsolationWindowTargetMz { get; init; }

    /// <summary>
    /// Isolation window lower offset (in m/z).
    /// </summary>
    public double IsolationWindowLowerOffset { get; init; }

    /// <summary>
    /// Isolation window upper offset (in m/z).
    /// </summary>
    public double IsolationWindowUpperOffset { get; init; }

    /// <summary>
    /// Isolation window width (lower + upper offset).
    /// </summary>
    public double IsolationWindowWidth => IsolationWindowLowerOffset + IsolationWindowUpperOffset;

    /// <summary>
    /// Charge state of the precursor ion (0 if unknown).
    /// </summary>
    public int Charge { get; init; }

    /// <summary>
    /// Activation method (e.g., "HCD", "CID", "ETD").
    /// </summary>
    public string ActivationMethod { get; init; } = string.Empty;

    /// <summary>
    /// Collision energy (normalized or absolute).
    /// </summary>
    public double CollisionEnergy { get; init; }

    /// <summary>
    /// Precursor intensity (if available).
    /// </summary>
    public double? Intensity { get; init; }

    /// <summary>
    /// Scan number of the precursor scan (0 if unknown).
    /// </summary>
    public int PrecursorScanNumber { get; init; }
}
