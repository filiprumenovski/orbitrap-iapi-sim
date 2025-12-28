namespace VirtualOrbitrap.Schema;

/// <summary>
/// MRM/SRM configuration.
/// </summary>
public class MRMInfo
{
    /// <summary>
    /// List of mass ranges monitored.
    /// </summary>
    public List<MRMMassRange> MRMMassList { get; set; } = new();
}

/// <summary>
/// Single MRM mass range.
/// </summary>
public struct MRMMassRange
{
    /// <summary>Start mass.</summary>
    public double StartMass { get; set; }

    /// <summary>End mass.</summary>
    public double EndMass { get; set; }

    /// <summary>Central mass.</summary>
    public double CentralMass { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{StartMass:F3}-{EndMass:F3}";
}
