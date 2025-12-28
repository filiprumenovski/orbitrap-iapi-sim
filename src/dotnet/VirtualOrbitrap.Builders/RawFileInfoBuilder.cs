using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.Builders;

/// <summary>
/// Builder for RawFileInfo (file-level metadata).
/// </summary>
public class RawFileInfoBuilder
{
    private readonly RawFileInfo _info = new();

    /// <summary>
    /// Set sample name.
    /// </summary>
    public RawFileInfoBuilder WithSampleName(string sampleName)
    {
        _info.SampleName = sampleName;
        return this;
    }

    /// <summary>
    /// Set instrument metadata.
    /// </summary>
    public RawFileInfoBuilder WithInstrument(
        string name = "Virtual Orbitrap",
        string model = "Virtual Orbitrap Simulator",
        string serialNumber = "SIM-001")
    {
        _info.InstName = name;
        _info.InstModel = model;
        _info.InstSerialNumber = serialNumber;
        return this;
    }

    /// <summary>
    /// Set scan range and timing.
    /// </summary>
    public RawFileInfoBuilder WithScanRange(
        int firstScan,
        int lastScan,
        double firstTimeMinutes,
        double lastTimeMinutes)
    {
        _info.ScanStart = firstScan;
        _info.ScanEnd = lastScan;
        _info.FirstScanTimeMinutes = firstTimeMinutes;
        _info.LastScanTimeMinutes = lastTimeMinutes;
        return this;
    }

    /// <summary>
    /// Set mass resolution.
    /// </summary>
    public RawFileInfoBuilder WithResolution(double resolution)
    {
        _info.MassResolution = resolution;
        return this;
    }

    /// <summary>
    /// Set file creation date.
    /// </summary>
    public RawFileInfoBuilder WithCreationDate(DateTime date)
    {
        _info.CreationDate = date;
        return this;
    }

    /// <summary>
    /// Set comment field.
    /// </summary>
    public RawFileInfoBuilder WithComment(string comment)
    {
        _info.Comment1 = comment;
        return this;
    }

    /// <summary>
    /// Build the RawFileInfo instance.
    /// </summary>
    public RawFileInfo Build()
    {
        if (_info.CreationDate == default)
            _info.CreationDate = DateTime.Now;
        return _info;
    }
}
