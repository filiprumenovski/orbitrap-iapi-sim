namespace VirtualOrbitrap.Enrichment;

/// <summary>
/// Generates realistic noise values for Orbitrap peaks.
/// Model: noise = sqrt(intensity) * shotFactor + electronicNoise
/// </summary>
public class NoiseSynthesizer
{
    private readonly Random _rng;

    /// <summary>
    /// Shot noise contribution factor.
    /// Higher = more noise relative to signal.
    /// Typical range: 0.01 - 0.05
    /// </summary>
    public double ShotNoiseFactor { get; set; } = 0.02;

    /// <summary>
    /// Electronic/baseline noise floor.
    /// Typical range: 50 - 500 counts.
    /// </summary>
    public double ElectronicNoiseFloor { get; set; } = 100;

    /// <summary>
    /// Random variation in electronic noise (0-1).
    /// </summary>
    public double NoiseVariance { get; set; } = 0.3;

    /// <summary>
    /// Create a noise synthesizer with an optional RNG seed.
    /// </summary>
    public NoiseSynthesizer(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Generate noise values for an intensity array.
    /// </summary>
    public double[] Generate(double[] intensities)
    {
        if (intensities == null || intensities.Length == 0)
            return Array.Empty<double>();

        var noises = new double[intensities.Length];
        for (int i = 0; i < intensities.Length; i++)
        {
            noises[i] = GenerateSingle(intensities[i]);
        }
        return noises;
    }

    /// <summary>
    /// Generate noise for a single intensity value.
    /// </summary>
    public double GenerateSingle(double intensity)
    {
        // Shot noise component: proportional to sqrt(intensity)
        double shotNoise = Math.Sqrt(Math.Abs(intensity)) * ShotNoiseFactor;

        // Electronic noise component: baseline with random variation
        double variation = 1.0 + ((_rng.NextDouble() - 0.5) * 2 * NoiseVariance);
        double electronicNoise = ElectronicNoiseFloor * variation;

        // Total noise (RSS combination)
        return Math.Sqrt(shotNoise * shotNoise + electronicNoise * electronicNoise);
    }
}
