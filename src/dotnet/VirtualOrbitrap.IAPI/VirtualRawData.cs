using VirtualOrbitrap.Builders;
using VirtualOrbitrap.Enrichment;
using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.IAPI;

/// <summary>
/// Implementation of IVirtualRawData.
/// Wraps loaded/simulated data and exposes via IAPI-compatible interface.
/// </summary>
public class VirtualRawData : IVirtualRawData
{
    private readonly Dictionary<int, CentroidStream> _centroidStreams = new();
    private readonly Dictionary<int, ScanInfo> _scanInfos = new();
    private readonly Dictionary<int, ScanStatistics> _scanStats = new();

    /// <summary>
    /// File-level metadata.
    /// </summary>
    public RawFileInfo FileInfo { get; private set; }

    /// <summary>
    /// First scan number in the file.
    /// </summary>
    public int ScanStart => FileInfo.ScanStart;

    /// <summary>
    /// Last scan number in the file.
    /// </summary>
    public int ScanEnd => FileInfo.ScanEnd == 0 ? _centroidStreams.Count : FileInfo.ScanEnd;

    /// <summary>
    /// Total number of scans.
    /// </summary>
    public int NumScans => ScanEnd - ScanStart + 1;

    /// <summary>
    /// Event raised when a new scan arrives.
    /// </summary>
    public event EventHandler<ScanArrivedEventArgs> ScanArrived;

    /// <summary>
    /// Create a new VirtualRawData instance.
    /// </summary>
    public VirtualRawData(RawFileInfo? fileInfo = null)
    {
        FileInfo = fileInfo ?? new RawFileInfoBuilder().Build();
    }

    /// <summary>
    /// Add a scan to the virtual file.
    /// </summary>
    public void AddScan(
        int scanNumber,
        CentroidStream centroidStream,
        ScanInfo scanInfo,
        ScanStatistics? stats = null)
    {
        _centroidStreams[scanNumber] = centroidStream;
        _scanInfos[scanNumber] = scanInfo;

        if (stats == null)
        {
            stats = ScanStatsCalculator.Calculate(centroidStream, scanInfo.RetentionTime);
        }
        _scanStats[scanNumber] = stats;

        // Update file info
        if (scanNumber > FileInfo.ScanEnd)
            FileInfo.ScanEnd = scanNumber;
        if (scanNumber < FileInfo.ScanStart || FileInfo.ScanStart == 0)
            FileInfo.ScanStart = scanNumber;
    }

    /// <summary>
    /// Get centroid stream for a scan.
    /// </summary>
    public CentroidStream GetCentroidStream(int scanNumber)
    {
        if (_centroidStreams.TryGetValue(scanNumber, out var stream))
            return stream;

        throw new KeyNotFoundException($"Scan {scanNumber} not found.");
    }

    /// <summary>
    /// Get scan metadata.
    /// </summary>
    public ScanInfo GetScanInfo(int scanNumber)
    {
        if (_scanInfos.TryGetValue(scanNumber, out var info))
            return info;

        throw new KeyNotFoundException($"Scan {scanNumber} not found.");
    }

    /// <summary>
    /// Get scan statistics.
    /// </summary>
    public ScanStatistics GetScanStatistics(int scanNumber)
    {
        if (_scanStats.TryGetValue(scanNumber, out var stats))
            return stats;

        throw new KeyNotFoundException($"Scan {scanNumber} not found.");
    }

    /// <summary>
    /// Get retention time for a scan.
    /// </summary>
    public double GetRetentionTime(int scanNumber)
    {
        return _scanInfos.TryGetValue(scanNumber, out var info)
            ? info.RetentionTime
            : 0;
    }

    /// <summary>
    /// Get MS level for a scan.
    /// </summary>
    public int GetMSLevel(int scanNumber)
    {
        return _scanInfos.TryGetValue(scanNumber, out var info)
            ? info.MSLevel
            : 0;
    }

    /// <summary>
    /// Get filter text for a scan.
    /// </summary>
    public string GetFilterText(int scanNumber)
    {
        return _scanInfos.TryGetValue(scanNumber, out var info)
            ? info.FilterText
            : string.Empty;
    }

    /// <summary>
    /// Get label data (alternative to CentroidStream for some uses).
    /// </summary>
    public FTLabelInfo[] GetScanLabelData(int scanNumber)
    {
        var stream = GetCentroidStream(scanNumber);

        var labels = new FTLabelInfo[stream.Length];
        for (int i = 0; i < stream.Length; i++)
        {
            labels[i] = new FTLabelInfo
            {
                Mass = stream.Masses[i],
                Intensity = stream.Intensities[i],
                Resolution = (float)(stream.Resolutions?[i] ?? 0),
                Baseline = (float)(stream.Baselines?[i] ?? 0),
                Noise = (float)(stream.Noises?[i] ?? 0),
                Charge = stream.Charges?[i] ?? 0
            };
        }
        return labels;
    }

    /// <summary>
    /// Get mass precision data.
    /// </summary>
    public MassPrecisionInfo[] GetScanPrecisionData(int scanNumber)
    {
        var stream = GetCentroidStream(scanNumber);

        var precision = new MassPrecisionInfo[stream.Length];
        for (int i = 0; i < stream.Length; i++)
        {
            precision[i] = new MassPrecisionInfo
            {
                Mass = stream.Masses[i],
                Intensity = stream.Intensities[i],
                Resolution = stream.Resolutions?[i] ?? 0,
                AccuracyPPM = 3.0,
                AccuracyMMU = stream.Masses[i] * 3.0 / 1e6
            };
        }
        return precision;
    }

    /// <summary>
    /// Raise ScanArrived event (for streaming simulation).
    /// </summary>
    public void EmitScan(int scanNumber)
    {
        if (!_centroidStreams.TryGetValue(scanNumber, out var stream))
            return;
        if (!_scanInfos.TryGetValue(scanNumber, out var info))
            return;

        ScanArrived?.Invoke(this, new ScanArrivedEventArgs
        {
            ScanNumber = scanNumber,
            RetentionTime = info.RetentionTime,
            MSLevel = info.MSLevel,
            CentroidStream = stream,
            ScanInfo = info
        });
    }

    /// <summary>
    /// Emit all scans with configurable delay (real-time simulation).
    /// </summary>
    public async Task EmitAllScansAsync(
        TimeSpan delayBetweenScans,
        CancellationToken cancellationToken = default)
    {
        for (int scan = ScanStart; scan <= ScanEnd; scan++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            EmitScan(scan);
            await Task.Delay(delayBetweenScans, cancellationToken);
        }
    }

    /// <summary>
    /// Release resources.
    /// </summary>
    public void Dispose()
    {
        _centroidStreams.Clear();
        _scanInfos.Clear();
        _scanStats.Clear();
    }
}
