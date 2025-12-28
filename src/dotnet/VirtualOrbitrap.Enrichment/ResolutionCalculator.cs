namespace VirtualOrbitrap.Enrichment;

/// <summary>
/// Calculates Orbitrap resolution using physics-based model.
/// Orbitrap resolution decreases with sqrt of m/z.
/// </summary>
public static class ResolutionCalculator
{
    /// <summary>
    /// Default resolution at reference mass (m/z 200).
    /// Common Orbitrap settings: 15K, 30K, 60K, 120K, 240K, 480K
    /// </summary>
    public const double DefaultR0 = 120000;

    /// <summary>
    /// Default reference mass for resolution specification.
    /// Thermo specifies resolution at m/z 200.
    /// </summary>
    public const double DefaultM0 = 200.0;

    /// <summary>
    /// Calculate resolution for an array of m/z values.
    /// Formula: R(m) = R0 * sqrt(m0 / m)
    /// </summary>
    /// <param name="masses">Array of m/z values</param>
    /// <param name="r0">Resolution at reference mass (default: 120,000)</param>
    /// <param name="m0">Reference mass (default: 200 m/z)</param>
    /// <returns>Array of resolution values</returns>
    public static double[] Calculate(double[] masses, double r0 = DefaultR0, double m0 = DefaultM0)
    {
        if (masses == null || masses.Length == 0)
            return Array.Empty<double>();

        var resolutions = new double[masses.Length];
        for (int i = 0; i < masses.Length; i++)
        {
            resolutions[i] = CalculateSingle(masses[i], r0, m0);
        }
        return resolutions;
    }

    /// <summary>
    /// Calculate resolution for a single m/z value.
    /// </summary>
    public static double CalculateSingle(double mass, double r0 = DefaultR0, double m0 = DefaultM0)
    {
        if (mass <= 0) return 0;
        return r0 * Math.Sqrt(m0 / mass);
    }

    /// <summary>
    /// Get resolution setting string for common Orbitrap configurations.
    /// </summary>
    public static string GetResolutionSettingName(double r0)
    {
        return r0 switch
        {
            >= 450000 => "480K",
            >= 200000 => "240K",
            >= 100000 => "120K",
            >= 50000 => "60K",
            >= 25000 => "30K",
            >= 12000 => "15K",
            _ => $"{r0 / 1000:F0}K"
        };
    }
}
