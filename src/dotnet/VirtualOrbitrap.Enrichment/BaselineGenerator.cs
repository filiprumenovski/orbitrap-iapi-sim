namespace VirtualOrbitrap.Enrichment;

/// <summary>
/// Generates baseline intensity values.
/// Model: slowly varying function with random fluctuations.
/// </summary>
public class BaselineGenerator
{
    private readonly Random _rng;

    /// <summary>
    /// Mean baseline level.
    /// Typical range: 100 - 1000 counts.
    /// </summary>
    public double MeanBaseline { get; set; } = 500;

    /// <summary>
    /// Baseline variance (absolute, not percentage).
    /// </summary>
    public double Variance { get; set; } = 100;

    /// <summary>
    /// Drift rate per peak (for slowly varying baseline).
    /// Set to 0 for flat baseline.
    /// </summary>
    public double DriftRate { get; set; } = 0;

    /// <summary>
    /// Create a baseline generator with an optional RNG seed.
    /// </summary>
    public BaselineGenerator(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Generate baseline values for a scan.
    /// </summary>
    public double[] Generate(int length)
    {
        if (length <= 0)
            return Array.Empty<double>();

        var baselines = new double[length];
        double currentLevel = MeanBaseline;

        for (int i = 0; i < length; i++)
        {
            // Add random fluctuation
            double fluctuation = (_rng.NextDouble() - 0.5) * 2 * Variance;
            baselines[i] = Math.Max(0, currentLevel + fluctuation);

            // Apply drift
            currentLevel += DriftRate;
        }

        return baselines;
    }
}
