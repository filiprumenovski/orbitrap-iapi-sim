using System.Buffers;
using Orbitrap.Abstractions;

namespace Orbitrap.Mock;

/// <summary>
/// Mock implementation of IOrbitrapScan for testing and simulation.
/// Uses ArrayPool for efficient memory management with high-frequency scan data.
/// </summary>
public sealed class MockMsScan : IOrbitrapScan, IDisposable
{
    private readonly double[] _mzValues;
    private readonly double[] _intensityValues;
    private readonly IReadOnlyDictionary<string, string> _trailerExtra;
    private readonly bool _rentedFromPool;
    private bool _disposed;

    /// <summary>
    /// Creates a MockMsScan with specified values.
    /// </summary>
    public MockMsScan(
        int scanNumber,
        int msOrder,
        double retentionTime,
        double[] mzValues,
        double[] intensityValues,
        double basePeakMz,
        double basePeakIntensity,
        double totalIonCurrent,
        double? precursorMass = null,
        int? precursorCharge = null,
        double? precursorIntensity = null,
        double? isolationWidth = null,
        double? collisionEnergy = null,
        FragmentationType? fragmentationType = null,
        string analyzer = "Orbitrap",
        double resolutionAtMz200 = 120000,
        double massAccuracyPpm = 3.0,
        Polarity polarity = Polarity.Positive,
        IReadOnlyDictionary<string, string>? trailerExtra = null,
        bool useArrayPool = false)
    {
        ScanNumber = scanNumber;
        MsOrder = msOrder;
        RetentionTime = retentionTime;

        if (useArrayPool && mzValues.Length > 0)
        {
            _mzValues = ArrayPool<double>.Shared.Rent(mzValues.Length);
            _intensityValues = ArrayPool<double>.Shared.Rent(intensityValues.Length);
            mzValues.CopyTo(_mzValues, 0);
            intensityValues.CopyTo(_intensityValues, 0);
            _rentedFromPool = true;
            PeakCount = mzValues.Length; // Track actual count since rented array may be larger
        }
        else
        {
            _mzValues = mzValues ?? [];
            _intensityValues = intensityValues ?? [];
            _rentedFromPool = false;
            PeakCount = _mzValues.Length;
        }

        BasePeakMz = basePeakMz;
        BasePeakIntensity = basePeakIntensity;
        TotalIonCurrent = totalIonCurrent;

        PrecursorMass = precursorMass;
        PrecursorCharge = precursorCharge;
        PrecursorIntensity = precursorIntensity;
        IsolationWidth = isolationWidth;
        CollisionEnergy = collisionEnergy;
        FragmentationType = fragmentationType;

        Analyzer = analyzer;
        ResolutionAtMz200 = resolutionAtMz200;
        MassAccuracyPpm = massAccuracyPpm;
        Polarity = polarity;

        _trailerExtra = trailerExtra ?? new Dictionary<string, string>();
    }

    public int ScanNumber { get; }
    public int MsOrder { get; }
    public double RetentionTime { get; }

    public ReadOnlyMemory<double> MzValues => _mzValues.AsMemory(0, PeakCount);
    public ReadOnlyMemory<double> IntensityValues => _intensityValues.AsMemory(0, PeakCount);
    public int PeakCount { get; }

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

    public FrozenOrbitrapScan ToFrozen() => new(this);

    public void Dispose()
    {
        if (_disposed) return;

        if (_rentedFromPool)
        {
            ArrayPool<double>.Shared.Return(_mzValues);
            ArrayPool<double>.Shared.Return(_intensityValues);
        }

        _disposed = true;
    }
}

/// <summary>
/// Builder for creating MockMsScan instances with fluent API.
/// </summary>
public sealed class MockMsScanBuilder
{
    private int _scanNumber = 1;
    private int _msOrder = 1;
    private double _retentionTime = 0.0;
    private double[] _mzValues = [];
    private double[] _intensityValues = [];
    private double _basePeakMz = 0.0;
    private double _basePeakIntensity = 0.0;
    private double _totalIonCurrent = 0.0;
    private double? _precursorMass;
    private int? _precursorCharge;
    private double? _precursorIntensity;
    private double? _isolationWidth;
    private double? _collisionEnergy;
    private FragmentationType? _fragmentationType;
    private string _analyzer = "Orbitrap";
    private double _resolutionAtMz200 = 120000;
    private double _massAccuracyPpm = 3.0;
    private Polarity _polarity = Polarity.Positive;
    private Dictionary<string, string>? _trailerExtra;
    private bool _useArrayPool = false;

    public MockMsScanBuilder WithScanNumber(int scanNumber)
    {
        _scanNumber = scanNumber;
        return this;
    }

    public MockMsScanBuilder WithMsOrder(int msOrder)
    {
        _msOrder = msOrder;
        return this;
    }

    public MockMsScanBuilder WithRetentionTime(double retentionTime)
    {
        _retentionTime = retentionTime;
        return this;
    }

    public MockMsScanBuilder WithSpectrum(double[] mzValues, double[] intensityValues)
    {
        _mzValues = mzValues;
        _intensityValues = intensityValues;

        // Auto-calculate aggregates
        if (mzValues.Length > 0 && intensityValues.Length > 0)
        {
            var maxIndex = 0;
            var maxIntensity = intensityValues[0];
            var tic = 0.0;

            for (int i = 0; i < intensityValues.Length; i++)
            {
                tic += intensityValues[i];
                if (intensityValues[i] > maxIntensity)
                {
                    maxIntensity = intensityValues[i];
                    maxIndex = i;
                }
            }

            _basePeakMz = mzValues[maxIndex];
            _basePeakIntensity = maxIntensity;
            _totalIonCurrent = tic;
        }

        return this;
    }

    public MockMsScanBuilder WithBasePeak(double mz, double intensity)
    {
        _basePeakMz = mz;
        _basePeakIntensity = intensity;
        return this;
    }

    public MockMsScanBuilder WithTotalIonCurrent(double tic)
    {
        _totalIonCurrent = tic;
        return this;
    }

    public MockMsScanBuilder WithPrecursor(
        double mass,
        int charge,
        double? intensity = null,
        double? isolationWidth = null,
        double? collisionEnergy = null,
        FragmentationType fragmentationType = Abstractions.FragmentationType.HCD)
    {
        _msOrder = 2;
        _precursorMass = mass;
        _precursorCharge = charge;
        _precursorIntensity = intensity;
        _isolationWidth = isolationWidth ?? 1.6;
        _collisionEnergy = collisionEnergy ?? 30.0;
        _fragmentationType = fragmentationType;
        return this;
    }

    public MockMsScanBuilder WithAnalyzer(string analyzer, double resolution = 120000, double massAccuracy = 3.0)
    {
        _analyzer = analyzer;
        _resolutionAtMz200 = resolution;
        _massAccuracyPpm = massAccuracy;
        return this;
    }

    public MockMsScanBuilder WithPolarity(Polarity polarity)
    {
        _polarity = polarity;
        return this;
    }

    public MockMsScanBuilder WithTrailerExtra(string key, string value)
    {
        _trailerExtra ??= new Dictionary<string, string>();
        _trailerExtra[key] = value;
        return this;
    }

    public MockMsScanBuilder UseArrayPool(bool usePool = true)
    {
        _useArrayPool = usePool;
        return this;
    }

    public MockMsScan Build()
    {
        return new MockMsScan(
            _scanNumber,
            _msOrder,
            _retentionTime,
            _mzValues,
            _intensityValues,
            _basePeakMz,
            _basePeakIntensity,
            _totalIonCurrent,
            _precursorMass,
            _precursorCharge,
            _precursorIntensity,
            _isolationWidth,
            _collisionEnergy,
            _fragmentationType,
            _analyzer,
            _resolutionAtMz200,
            _massAccuracyPpm,
            _polarity,
            _trailerExtra,
            _useArrayPool);
    }
}
