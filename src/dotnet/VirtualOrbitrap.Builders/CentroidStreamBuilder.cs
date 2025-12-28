using VirtualOrbitrap.Enrichment;
using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.Builders;

/// <summary>
/// Fluent builder for CentroidStream objects.
/// </summary>
public class CentroidStreamBuilder
{
    private readonly CentroidStream _stream = new();
    private readonly NoiseSynthesizer _noiseSynth;
    private readonly BaselineGenerator _baselineGen;
    private readonly double _defaultR0;

    /// <summary>
    /// Create a builder with optional resolution and RNG seed parameters.
    /// </summary>
    public CentroidStreamBuilder(
        double resolutionR0 = ResolutionCalculator.DefaultR0,
        int? randomSeed = null)
    {
        _defaultR0 = resolutionR0;
        _noiseSynth = new NoiseSynthesizer(randomSeed);
        _baselineGen = new BaselineGenerator(randomSeed);
    }

    /// <summary>
    /// Set the scan number.
    /// </summary>
    public CentroidStreamBuilder WithScanNumber(int scanNumber)
    {
        _stream.ScanNumber = scanNumber;
        return this;
    }

    /// <summary>
    /// Set the peak data (masses and intensities).
    /// </summary>
    public CentroidStreamBuilder WithPeaks(double[] masses, double[] intensities)
    {
        ArgumentNullException.ThrowIfNull(masses);
        ArgumentNullException.ThrowIfNull(intensities);

        if (masses.Length != intensities.Length)
            throw new ArgumentException("Masses and intensities must have same length");

        _stream.Masses = masses;
        _stream.Intensities = intensities;
        _stream.Length = masses.Length;
        return this;
    }

    /// <summary>
    /// Calculate and set resolution values using Orbitrap physics model.
    /// </summary>
    public CentroidStreamBuilder WithCalculatedResolutions(double r0 = ResolutionCalculator.DefaultR0, double m0 = ResolutionCalculator.DefaultM0)
    {
        if (_stream.Masses == null)
            throw new InvalidOperationException("Must set peaks before calculating resolutions");

        _stream.Resolutions = ResolutionCalculator.Calculate(_stream.Masses, r0, m0);
        return this;
    }

    /// <summary>
    /// Set explicit resolution values.
    /// </summary>
    public CentroidStreamBuilder WithResolutions(double[] resolutions)
    {
        _stream.Resolutions = resolutions;
        return this;
    }

    /// <summary>
    /// Generate and set noise values.
    /// </summary>
    public CentroidStreamBuilder WithSynthesizedNoise(
        double shotFactor = 0.02,
        double electronicFloor = 100)
    {
        if (_stream.Intensities == null)
            throw new InvalidOperationException("Must set peaks before synthesizing noise");

        _noiseSynth.ShotNoiseFactor = shotFactor;
        _noiseSynth.ElectronicNoiseFloor = electronicFloor;
        _stream.Noises = _noiseSynth.Generate(_stream.Intensities);
        return this;
    }

    /// <summary>
    /// Set explicit noise values.
    /// </summary>
    public CentroidStreamBuilder WithNoises(double[] noises)
    {
        _stream.Noises = noises;
        return this;
    }

    /// <summary>
    /// Generate and set baseline values.
    /// </summary>
    public CentroidStreamBuilder WithSynthesizedBaseline(
        double meanLevel = 500,
        double variance = 100)
    {
        if (_stream.Length <= 0)
            throw new InvalidOperationException("Must set peaks before synthesizing baseline");

        _baselineGen.MeanBaseline = meanLevel;
        _baselineGen.Variance = variance;
        _stream.Baselines = _baselineGen.Generate(_stream.Length);
        return this;
    }

    /// <summary>
    /// Set explicit baseline values.
    /// </summary>
    public CentroidStreamBuilder WithBaselines(double[] baselines)
    {
        _stream.Baselines = baselines;
        return this;
    }

    /// <summary>
    /// Set charge states (use 0 for undetermined).
    /// </summary>
    public CentroidStreamBuilder WithCharges(int[] charges)
    {
        _stream.Charges = charges;
        return this;
    }

    /// <summary>
    /// Set all charge states to 0 (undetermined).
    /// Use this for MVP when charge estimation is deferred.
    /// </summary>
    public CentroidStreamBuilder WithUndeterminedCharges()
    {
        if (_stream.Length <= 0)
            throw new InvalidOperationException("Must set peaks before setting charges");

        _stream.Charges = new int[_stream.Length];
        return this;
    }

    /// <summary>
    /// Set calibration coefficients.
    /// </summary>
    public CentroidStreamBuilder WithCalibration(double[] coefficients)
    {
        _stream.Coefficients = coefficients;
        _stream.CoefficientsCount = coefficients?.Length ?? 0;
        return this;
    }

    /// <summary>
    /// Build the CentroidStream with validation.
    /// </summary>
    public CentroidStream Build()
    {
        // Ensure required fields
        if (_stream.Masses == null || _stream.Intensities == null)
            throw new InvalidOperationException("Masses and intensities are required");

        // Generate defaults for missing fields
        if (_stream.Resolutions == null)
            WithCalculatedResolutions(_defaultR0);
        if (_stream.Noises == null)
            WithSynthesizedNoise();
        if (_stream.Baselines == null)
            WithSynthesizedBaseline();
        if (_stream.Charges == null)
            WithUndeterminedCharges();

        // Validate
        if (!_stream.Validate())
            throw new InvalidOperationException("CentroidStream validation failed");

        return _stream;
    }
}
