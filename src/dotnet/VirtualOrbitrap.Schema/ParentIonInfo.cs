namespace VirtualOrbitrap.Schema;

/// <summary>
/// Parent ion fragmentation information.
/// One entry per fragmentation step (MS2 has 1, MS3 has 2, etc.)
/// </summary>
public struct ParentIonInfo
{
    /// <summary>
    /// MS level of THIS spectrum (not the parent).
    /// </summary>
    public int MSLevel { get; set; }

    /// <summary>
    /// Parent ion m/z that was fragmented.
    /// </summary>
    public double ParentIonMZ { get; set; }

    /// <summary>
    /// Primary collision/activation mode.
    /// Lowercase: "cid", "hcd", "etd", "ethcd", "etcid"
    /// </summary>
    public string CollisionMode { get; set; }

    /// <summary>
    /// Secondary collision mode (for dual activation like EThcD).
    /// </summary>
    public string CollisionMode2 { get; set; }

    /// <summary>
    /// Primary collision energy (normalized or absolute).
    /// Typical range: 20-40 for HCD, 25-35 for CID.
    /// </summary>
    public float CollisionEnergy { get; set; }

    /// <summary>
    /// Secondary collision energy (for dual activation).
    /// </summary>
    public float CollisionEnergy2 { get; set; }

    /// <summary>
    /// Activation type enum value.
    /// </summary>
    public ActivationType ActivationType { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(CollisionMode))
            return $"ms{MSLevel} {ParentIonMZ:F2}";
        return $"ms{MSLevel} {ParentIonMZ:F2}@{CollisionMode}{CollisionEnergy:F2}";
    }
}
