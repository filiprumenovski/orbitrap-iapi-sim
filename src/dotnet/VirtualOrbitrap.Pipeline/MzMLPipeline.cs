using System.Runtime.CompilerServices;
using VirtualOrbitrap.Builders;
using VirtualOrbitrap.IAPI;
using VirtualOrbitrap.Parsers;
using VirtualOrbitrap.Parsers.Dto;
using VirtualOrbitrap.Schema;

namespace VirtualOrbitrap.Pipeline;

/// <summary>
/// End-to-end pipeline that loads mzML files and produces VirtualRawData.
/// Coordinates: Parser → Converter → Builders → VirtualRawData.
/// </summary>
public sealed class MzMLPipeline
{
    private readonly IMzMLLoader _loader;
    private readonly ScanConverter _converter;
    private readonly PipelineOptions _options;

    /// <summary>
    /// Create a pipeline with default mzML loader and options.
    /// </summary>
    public MzMLPipeline(PipelineOptions? options = null)
        : this(new MzMLLoader(), options)
    {
    }

    /// <summary>
    /// Create a pipeline with custom loader and options.
    /// </summary>
    public MzMLPipeline(IMzMLLoader loader, PipelineOptions? options = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _options = options ?? new PipelineOptions();
        _converter = new ScanConverter(_options);
    }

    /// <summary>
    /// Load an mzML file and convert to VirtualRawData.
    /// Loads entire file into memory.
    /// </summary>
    /// <param name="mzmlPath">Path to mzML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>VirtualRawData populated with all scans.</returns>
    public async Task<VirtualRawData> LoadAsync(string mzmlPath, CancellationToken cancellationToken = default)
    {
        // Parse mzML file
        var parsedFile = await _loader.LoadAsync(mzmlPath, cancellationToken);

        // Build file-level metadata
        var fileInfo = new RawFileInfoBuilder()
            .WithSampleName(parsedFile.FileName)
            .WithInstrument(
                name: parsedFile.InstrumentModel,
                model: parsedFile.InstrumentModel,
                serialNumber: parsedFile.InstrumentSerialNumber)
            .WithScanRange(
                parsedFile.FirstScanNumber,
                parsedFile.LastScanNumber,
                parsedFile.StartTime,
                parsedFile.EndTime)
            .WithResolution(_options.ResolutionR0)
            .WithCreationDate(parsedFile.CreationDate ?? DateTime.Now)
            .WithComment($"Converted from {parsedFile.FileName}")
            .Build();

        // Create VirtualRawData container
        var rawData = new VirtualRawData(fileInfo);

        // Convert and add all scans
        foreach (var scan in parsedFile.Scans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (centroidStream, scanInfo) = _converter.Convert(scan);
            rawData.AddScan(scan.ScanNumber, centroidStream, scanInfo);
        }

        return rawData;
    }

    /// <summary>
    /// Stream scans from mzML file with optional replay timing.
    /// Use for real-time simulation or memory-efficient processing.
    /// </summary>
    /// <param name="mzmlPath">Path to mzML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of converted scans with timing.</returns>
    public async IAsyncEnumerable<(CentroidStream Stream, ScanInfo Info)> StreamAsync(
        string mzmlPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        double? previousRt = null;

        await foreach (var scan in _loader.StreamScansAsync(mzmlPath, cancellationToken))
        {
            // Apply replay delay based on mode
            await ApplyReplayDelayAsync(scan.RetentionTimeMinutes, previousRt, cancellationToken);
            previousRt = scan.RetentionTimeMinutes;

            // Convert and yield
            var converted = _converter.Convert(scan);
            yield return converted;
        }
    }

    /// <summary>
    /// Stream scans and emit ScanArrived events on the provided VirtualRawData.
    /// </summary>
    /// <param name="mzmlPath">Path to mzML file.</param>
    /// <param name="rawData">VirtualRawData to populate and emit events from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StreamWithEventsAsync(
        string mzmlPath,
        VirtualRawData rawData,
        CancellationToken cancellationToken = default)
    {
        double? previousRt = null;

        await foreach (var scan in _loader.StreamScansAsync(mzmlPath, cancellationToken))
        {
            // Apply replay delay based on mode
            await ApplyReplayDelayAsync(scan.RetentionTimeMinutes, previousRt, cancellationToken);
            previousRt = scan.RetentionTimeMinutes;

            // Convert scan
            var (centroidStream, scanInfo) = _converter.Convert(scan);

            // Add to raw data
            rawData.AddScan(scan.ScanNumber, centroidStream, scanInfo);
            
            // Emit ScanArrived event
            rawData.EmitScan(scan.ScanNumber);
        }
    }

    private async Task ApplyReplayDelayAsync(
        double currentRt,
        double? previousRt,
        CancellationToken cancellationToken)
    {
        switch (_options.ReplayMode)
        {
            case ReplayMode.Immediate:
                // No delay
                break;

            case ReplayMode.RealTime when previousRt.HasValue:
                // Calculate delay based on RT difference
                var rtDeltaMinutes = currentRt - previousRt.Value;
                if (rtDeltaMinutes > 0)
                {
                    var delayMs = (int)(rtDeltaMinutes * 60_000 * _options.ReplayDelayMultiplier);
                    await Task.Delay(Math.Min(delayMs, 10_000), cancellationToken); // Cap at 10s
                }
                break;

            case ReplayMode.FixedDelay:
                await Task.Delay(_options.FixedDelayMs, cancellationToken);
                break;
        }
    }
}
