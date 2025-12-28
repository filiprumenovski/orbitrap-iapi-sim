using VirtualOrbitrap.Enrichment;
using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.Builders;

/// <summary>
/// Fluent builder for ScanInfo objects.
/// </summary>
public class ScanInfoBuilder
{
    private readonly ScanInfo _scanInfo = new();

    /// <summary>
    /// Set the scan number.
    /// </summary>
    public ScanInfoBuilder WithScanNumber(int scanNumber)
    {
        _scanInfo.ScanNumber = scanNumber;
        return this;
    }

    /// <summary>
    /// Set the MS level.
    /// </summary>
    public ScanInfoBuilder WithMSLevel(int msLevel)
    {
        _scanInfo.MSLevel = msLevel;
        return this;
    }

    /// <summary>
    /// Set retention time in minutes.
    /// </summary>
    public ScanInfoBuilder WithRetentionTime(double retentionTimeMinutes)
    {
        _scanInfo.RetentionTime = retentionTimeMinutes;
        return this;
    }

    /// <summary>
    /// Set precursor values for MS2+ scans.
    /// </summary>
    public ScanInfoBuilder WithPrecursor(
        double precursorMz,
        int charge = 0,
        double isolationWidth = 2.0,
        int parentScanNumber = 0)
    {
        _scanInfo.ParentIonMZ = precursorMz;
        _scanInfo.ParentIonMonoisotopicMZ = precursorMz;
        _scanInfo.IsolationWindowWidthMZ = isolationWidth;
        _scanInfo.ParentScan = parentScanNumber;

        // Add to scan events
        _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
            "Monoisotopic M/Z:", precursorMz.ToString("F6")));
        if (charge > 0)
        {
            _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
                "Charge State:", charge.ToString()));
        }
        _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
            "MS2 Isolation Width:", isolationWidth.ToString("F2")));

        return this;
    }

    /// <summary>
    /// Set fragmentation properties.
    /// </summary>
    public ScanInfoBuilder WithFragmentation(
        ActivationType activationType,
        double collisionEnergy)
    {
        _scanInfo.ActivationType = activationType;
        _scanInfo.CollisionMode = activationType switch
        {
            ActivationType.HCD => "hcd",
            ActivationType.CID => "cid",
            ActivationType.ETD => "etd",
            ActivationType.ECD => "ecd",
            _ => "hcd"
        };

        // Add parent ion info
        var parentIon = new ParentIonInfo
        {
            MSLevel = _scanInfo.MSLevel,
            ParentIonMZ = _scanInfo.ParentIonMZ,
            CollisionMode = _scanInfo.CollisionMode,
            CollisionEnergy = (float)collisionEnergy,
            ActivationType = activationType
        };
        _scanInfo.ParentIons.Add(parentIon);

        return this;
    }

    /// <summary>
    /// Set ionization polarity.
    /// </summary>
    public ScanInfoBuilder WithPolarity(IonMode polarity)
    {
        _scanInfo.IonMode = polarity;
        return this;
    }

    /// <summary>
    /// Mark as centroided or profile data.
    /// </summary>
    public ScanInfoBuilder AsCentroid(bool isCentroid = true)
    {
        _scanInfo.IsCentroided = isCentroid;
        return this;
    }

    /// <summary>
    /// Mark as high-resolution data.
    /// </summary>
    public ScanInfoBuilder AsHighResolution(bool isHighRes = true)
    {
        _scanInfo.IsHighResolution = isHighRes;
        return this;
    }

    /// <summary>
    /// Set scan statistics explicitly.
    /// </summary>
    public ScanInfoBuilder WithStatistics(
        int numPeaks,
        double tic,
        double basePeakMz,
        double basePeakIntensity,
        double lowMass,
        double highMass)
    {
        _scanInfo.NumPeaks = numPeaks;
        _scanInfo.TotalIonCurrent = tic;
        _scanInfo.BasePeakMZ = basePeakMz;
        _scanInfo.BasePeakIntensity = basePeakIntensity;
        _scanInfo.LowMass = lowMass;
        _scanInfo.HighMass = highMass;
        return this;
    }

    /// <summary>
    /// Set scan statistics from ScanStatistics.
    /// </summary>
    public ScanInfoBuilder WithStatistics(ScanStatistics stats)
    {
        _scanInfo.NumPeaks = (int)stats.TIC; // Approximate
        _scanInfo.TotalIonCurrent = stats.TIC;
        _scanInfo.BasePeakMZ = stats.BasePeakMass;
        _scanInfo.BasePeakIntensity = stats.BasePeakIntensity;
        _scanInfo.LowMass = stats.LowMass;
        _scanInfo.HighMass = stats.HighMass;
        return this;
    }

    /// <summary>
    /// Set filter text directly.
    /// </summary>
    public ScanInfoBuilder WithFilterText(string filterText)
    {
        _scanInfo.FilterText = filterText;
        return this;
    }

    /// <summary>
    /// Generate filter text based on current settings.
    /// </summary>
    public ScanInfoBuilder WithGeneratedFilterText(
        double lowMass = 100,
        double highMass = 2000,
        double collisionEnergy = 30)
    {
        if (_scanInfo.MSLevel == 1)
        {
            _scanInfo.FilterText = FilterStringGenerator.GenerateMS1(
                _scanInfo.IonMode,
                _scanInfo.IsCentroided,
                lowMass,
                highMass);
        }
        else
        {
            _scanInfo.FilterText = FilterStringGenerator.GenerateMS2(
                _scanInfo.ParentIonMZ,
                _scanInfo.ActivationType,
                collisionEnergy,
                _scanInfo.IonMode,
                _scanInfo.IsCentroided,
                lowMass,
                highMass);
        }
        return this;
    }

    /// <summary>
    /// Set ion injection time and store trailer event.
    /// </summary>
    public ScanInfoBuilder WithIonInjectionTime(double milliseconds)
    {
        _scanInfo.IonInjectionTime = milliseconds;
        _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(
            "Ion Injection Time (ms):", milliseconds.ToString("F2")));
        return this;
    }

    /// <summary>
    /// Add a scan event.
    /// </summary>
    public ScanInfoBuilder WithScanEvent(string name, string value)
    {
        _scanInfo.ScanEvents.Add(new KeyValuePair<string, string>(name, value));
        return this;
    }

    /// <summary>
    /// Mark scan as DIA.
    /// </summary>
    public ScanInfoBuilder AsDIA(bool isDIA = true)
    {
        _scanInfo.IsDIA = isDIA;
        return this;
    }

    /// <summary>
    /// Mark scan as SIM.
    /// </summary>
    public ScanInfoBuilder AsSIM(bool isSIM = true)
    {
        _scanInfo.SIMScan = isSIM;
        if (isSIM) _scanInfo.MRMScanType = MRMScanType.SIM;
        return this;
    }

    /// <summary>
    /// Set scan statistics (TIC, base peak, peak count).
    /// </summary>
    public ScanInfoBuilder WithScanStatistics(
        double tic,
        double basePeakMz,
        double basePeakIntensity,
        int numPeaks)
    {
        _scanInfo.TotalIonCurrent = tic;
        _scanInfo.BasePeakMZ = basePeakMz;
        _scanInfo.BasePeakIntensity = basePeakIntensity;
        _scanInfo.NumPeaks = numPeaks;
        return this;
    }

    /// <summary>
    /// Set mass range (low/high m/z).
    /// </summary>
    public ScanInfoBuilder WithMassRange(double lowMass, double highMass)
    {
        _scanInfo.LowMass = lowMass;
        _scanInfo.HighMass = highMass;
        return this;
    }

    /// <summary>
    /// Generate filter string with full parameters.
    /// </summary>
    public ScanInfoBuilder WithGeneratedFilterString(
        IonMode polarity = IonMode.Positive,
        bool isCentroid = true,
        string ionizationMode = "NSI")
    {
        _scanInfo.IonMode = polarity;
        _scanInfo.IsCentroided = isCentroid;

        if (_scanInfo.MSLevel == 1)
        {
            _scanInfo.FilterText = FilterStringGenerator.GenerateMS1(
                polarity,
                isCentroid,
                _scanInfo.LowMass > 0 ? _scanInfo.LowMass : 100,
                _scanInfo.HighMass > 0 ? _scanInfo.HighMass : 2000);
        }
        else
        {
            double collisionEnergy = _scanInfo.ParentIons.Count > 0
                ? _scanInfo.ParentIons[0].CollisionEnergy
                : 30;

            _scanInfo.FilterText = FilterStringGenerator.GenerateMS2(
                _scanInfo.ParentIonMZ,
                _scanInfo.ActivationType,
                collisionEnergy,
                polarity,
                isCentroid,
                _scanInfo.LowMass > 0 ? _scanInfo.LowMass : 100,
                _scanInfo.HighMass > 0 ? _scanInfo.HighMass : 2000);
        }
        return this;
    }

    /// <summary>
    /// Build the ScanInfo instance.
    /// </summary>
    public ScanInfo Build()
    {
        // Set defaults
        if (_scanInfo.MSLevel == 0) _scanInfo.MSLevel = 1;
        if (_scanInfo.EventNumber == 0) _scanInfo.EventNumber = 1;

        // Auto-generate filter text if not set
        if (string.IsNullOrEmpty(_scanInfo.FilterText))
        {
            WithGeneratedFilterText();
        }

        return _scanInfo;
    }
}
