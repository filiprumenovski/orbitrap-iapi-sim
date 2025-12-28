using VirtualOrbitrap.Builders;
using VirtualOrbitrap.Enrichment;
using VirtualOrbitrap.Parsers.Dto;
using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.Pipeline;

/// <summary>
/// Converts parsed scans to enriched VirtualOrbitrap schema objects.
/// Handles resolution/noise/baseline/filter-string synthesis.
/// </summary>
public sealed class ScanConverter
{
    private readonly PipelineOptions _options;
    private readonly NoiseSynthesizer _noiseSynthesizer;
    private readonly BaselineGenerator _baselineGenerator;

    /// <summary>
    /// Create a scan converter with the given options.
    /// </summary>
    public ScanConverter(PipelineOptions? options = null)
    {
        _options = options ?? new PipelineOptions();
        _noiseSynthesizer = new NoiseSynthesizer(_options.RandomSeed)
        {
            ShotNoiseFactor = _options.ShotNoiseFactor,
            ElectronicNoiseFloor = _options.ElectronicNoiseFloor
        };
        _baselineGenerator = new BaselineGenerator(_options.RandomSeed)
        {
            DriftRate = _options.BaselineDriftCoeff
        };
    }

    /// <summary>
    /// Convert a parsed scan to a CentroidStream with enriched data.
    /// </summary>
    public CentroidStream ConvertToCentroidStream(ParsedScan scan)
    {
        var builder = new CentroidStreamBuilder(_options.ResolutionR0, _options.RandomSeed)
            .WithScanNumber(scan.ScanNumber)
            .WithPeaks(scan.Mzs, scan.Intensities);

        if (_options.CalculateResolutions)
        {
            builder.WithCalculatedResolutions(_options.ResolutionR0, _options.ResolutionM0);
        }

        if (_options.SynthesizeNoise)
        {
            builder.WithSynthesizedNoise(_options.ShotNoiseFactor, _options.ElectronicNoiseFloor);
        }

        if (_options.SynthesizeBaseline)
        {
            builder.WithSynthesizedBaseline(_options.BaselineDriftCoeff * 500, 100);
        }

        return builder.Build();
    }

    /// <summary>
    /// Convert a parsed scan to a ScanInfo with enriched metadata.
    /// </summary>
    public ScanInfo ConvertToScanInfo(ParsedScan scan)
    {
        var builder = new ScanInfoBuilder()
            .WithScanNumber(scan.ScanNumber)
            .WithMSLevel(scan.MsLevel)
            .WithRetentionTime(scan.RetentionTimeMinutes)
            .WithScanStatistics(
                scan.TotalIonCurrent,
                scan.BasePeakMz,
                scan.BasePeakIntensity,
                scan.PeakCount)
            .WithMassRange(scan.LowMz, scan.HighMz)
            .WithPolarity(scan.Polarity > 0 ? IonMode.Positive : IonMode.Negative);

        // Add precursor info for MS2+ scans
        if (scan.Precursor != null)
        {
            var activationType = ParseActivationType(scan.Precursor.ActivationMethod);

            builder.WithPrecursor(
                scan.Precursor.SelectedMz,
                scan.Precursor.Charge,
                scan.Precursor.IsolationWindowWidth,
                scan.Precursor.PrecursorScanNumber)
            .WithFragmentation(activationType, scan.Precursor.CollisionEnergy);
        }

        // Set injection time if available
        if (scan.InjectionTimeMs.HasValue)
        {
            builder.WithIonInjectionTime(scan.InjectionTimeMs.Value);
        }

        // Generate filter string
        if (_options.GenerateFilterStrings)
        {
            builder.WithGeneratedFilterString(
                scan.Polarity > 0 ? IonMode.Positive : IonMode.Negative,
                scan.IsCentroid,
                _options.IonizationMode);
        }

        return builder.Build();
    }

    /// <summary>
    /// Convert parsed scan to both CentroidStream and ScanInfo.
    /// </summary>
    public (CentroidStream CentroidStream, ScanInfo ScanInfo) Convert(ParsedScan scan)
    {
        return (ConvertToCentroidStream(scan), ConvertToScanInfo(scan));
    }

    private static ActivationType ParseActivationType(string method) => method.ToUpperInvariant() switch
    {
        "HCD" => ActivationType.HCD,
        "CID" => ActivationType.CID,
        "ETD" => ActivationType.ETD,
        "ECD" => ActivationType.ECD,
        "ETHCD" or "ETCID" => ActivationType.HCD, // Hybrid mapped to HCD
        "UVPD" => ActivationType.UVPD,
        "PQD" => ActivationType.PQD,
        _ => ActivationType.Unknown
    };
}
