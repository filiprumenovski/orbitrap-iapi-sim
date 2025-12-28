namespace VirtualOrbitrap.Pipeline;

/// <summary>
/// Configuration options for the Virtual Orbitrap pipeline.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>
    /// Base resolution at reference mass (R0).
    /// Default 60000 @ 200 m/z typical for Orbitrap.
    /// </summary>
    public double ResolutionR0 { get; set; } = 60000;

    /// <summary>
    /// Reference mass for resolution calculation (m0).
    /// </summary>
    public double ResolutionM0 { get; set; } = 200;

    /// <summary>
    /// Shot noise factor for intensity-dependent noise.
    /// </summary>
    public double ShotNoiseFactor { get; set; } = 0.02;

    /// <summary>
    /// Electronic noise floor value.
    /// </summary>
    public double ElectronicNoiseFloor { get; set; } = 100;

    /// <summary>
    /// Baseline drift coefficient.
    /// </summary>
    public double BaselineDriftCoeff { get; set; } = 0.001;

    /// <summary>
    /// Random seed for reproducible noise/baseline generation.
    /// Null for random.
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Default ionization mode string for filter strings.
    /// </summary>
    public string IonizationMode { get; set; } = "NSI";

    /// <summary>
    /// Whether to generate synthetic filter strings from scan data.
    /// </summary>
    public bool GenerateFilterStrings { get; set; } = true;

    /// <summary>
    /// Whether to synthesize noise arrays.
    /// </summary>
    public bool SynthesizeNoise { get; set; } = true;

    /// <summary>
    /// Whether to synthesize baseline arrays.
    /// </summary>
    public bool SynthesizeBaseline { get; set; } = true;

    /// <summary>
    /// Whether to calculate resolution arrays.
    /// </summary>
    public bool CalculateResolutions { get; set; } = true;

    /// <summary>
    /// Replay mode for streaming simulations.
    /// </summary>
    public ReplayMode ReplayMode { get; set; } = ReplayMode.Immediate;

    /// <summary>
    /// Delay multiplier for real-time replay mode.
    /// 1.0 = real-time, 0.5 = 2x speed, 2.0 = half speed.
    /// </summary>
    public double ReplayDelayMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Fixed delay in milliseconds between scans for FixedDelay mode.
    /// </summary>
    public int FixedDelayMs { get; set; } = 100;
}

/// <summary>
/// Replay timing mode for streaming simulations.
/// </summary>
public enum ReplayMode
{
    /// <summary>
    /// Emit scans as fast as possible.
    /// </summary>
    Immediate,

    /// <summary>
    /// Replay with delays matching original retention time gaps.
    /// </summary>
    RealTime,

    /// <summary>
    /// Fixed delay between each scan.
    /// </summary>
    FixedDelay
}
