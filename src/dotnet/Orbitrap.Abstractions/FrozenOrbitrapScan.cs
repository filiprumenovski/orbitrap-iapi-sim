using System.Collections.Frozen;

namespace Orbitrap.Abstractions;

/// <summary>
/// Immutable, thread-safe snapshot of an Orbitrap scan.
/// All data is defensively copied and uses FrozenDictionary for optimal read performance.
/// </summary>
public sealed class FrozenOrbitrapScan : IOrbitrapScan
{
    private readonly double[] _mzValues;
    private readonly double[] _intensityValues;
    private readonly FrozenDictionary<string, string> _trailerExtra;

    /// <summary>
    /// Creates an immutable snapshot from any IOrbitrapScan implementation.
    /// </summary>
    public FrozenOrbitrapScan(IOrbitrapScan source)
    {
        ArgumentNullException.ThrowIfNull(source);

        ScanNumber = source.ScanNumber;
        MsOrder = source.MsOrder;
        RetentionTime = source.RetentionTime;

        // Defensive copy of spectrum data
        _mzValues = source.MzValues.ToArray();
        _intensityValues = source.IntensityValues.ToArray();

        BasePeakMz = source.BasePeakMz;
        BasePeakIntensity = source.BasePeakIntensity;
        TotalIonCurrent = source.TotalIonCurrent;

        PrecursorMass = source.PrecursorMass;
        PrecursorCharge = source.PrecursorCharge;
        PrecursorIntensity = source.PrecursorIntensity;
        IsolationWidth = source.IsolationWidth;
        CollisionEnergy = source.CollisionEnergy;
        FragmentationType = source.FragmentationType;

        Analyzer = source.Analyzer;
        ResolutionAtMz200 = source.ResolutionAtMz200;
        MassAccuracyPpm = source.MassAccuracyPpm;
        Polarity = source.Polarity;

        // FrozenDictionary for optimal read performance
        _trailerExtra = source.TrailerExtra.ToFrozenDictionary();
    }

    /// <summary>
    /// Creates a FrozenOrbitrapScan with explicit values.
    /// </summary>
    public FrozenOrbitrapScan(
        int scanNumber,
        int msOrder,
        double retentionTime,
        double[] mzValues,
        double[] intensityValues,
        double basePeakMz,
        double basePeakIntensity,
        double totalIonCurrent,
        double? precursorMass,
        int? precursorCharge,
        double? precursorIntensity,
        double? isolationWidth,
        double? collisionEnergy,
        FragmentationType? fragmentationType,
        string analyzer,
        double resolutionAtMz200,
        double massAccuracyPpm,
        Polarity polarity,
        IReadOnlyDictionary<string, string>? trailerExtra = null)
    {
        ScanNumber = scanNumber;
        MsOrder = msOrder;
        RetentionTime = retentionTime;

        // Defensive copies
        _mzValues = mzValues?.ToArray() ?? [];
        _intensityValues = intensityValues?.ToArray() ?? [];

        BasePeakMz = basePeakMz;
        BasePeakIntensity = basePeakIntensity;
        TotalIonCurrent = totalIonCurrent;

        PrecursorMass = precursorMass;
        PrecursorCharge = precursorCharge;
        PrecursorIntensity = precursorIntensity;
        IsolationWidth = isolationWidth;
        CollisionEnergy = collisionEnergy;
        FragmentationType = fragmentationType;

        Analyzer = analyzer ?? "Unknown";
        ResolutionAtMz200 = resolutionAtMz200;
        MassAccuracyPpm = massAccuracyPpm;
        Polarity = polarity;

        _trailerExtra = (trailerExtra ?? new Dictionary<string, string>()).ToFrozenDictionary();
    }

    public int ScanNumber { get; }
    public int MsOrder { get; }
    public double RetentionTime { get; }

    public ReadOnlyMemory<double> MzValues => _mzValues;
    public ReadOnlyMemory<double> IntensityValues => _intensityValues;
    public int PeakCount => _mzValues.Length;

    public double BasePeakMz { get; }
    public double BasePeakIntensity { get; }
    public double TotalIonCurrent { get; }

    public double? PrecursorMass { get; }
    public int? PrecursorCharge { get; }
    public double? PrecursorIntensity { get; }
    public double? IsolationWidth { get; }
    public double? CollisionEnergy { get; }
    public FragmentationType? FragmentationType { get; }

    public string Analyzer { get; }
    public double ResolutionAtMz200 { get; }
    public double MassAccuracyPpm { get; }
    public Polarity Polarity { get; }

    public IReadOnlyDictionary<string, string> TrailerExtra => _trailerExtra;

    /// <summary>
    /// Returns itself since FrozenOrbitrapScan is already immutable.
    /// </summary>
    public FrozenOrbitrapScan ToFrozen() => this;
}
