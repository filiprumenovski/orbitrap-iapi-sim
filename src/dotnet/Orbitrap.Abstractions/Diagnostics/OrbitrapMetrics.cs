using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Orbitrap.Abstractions.Diagnostics;

/// <summary>
/// OpenTelemetry-compatible metrics for Orbitrap instrumentation.
/// </summary>
public static class OrbitrapMetrics
{
    /// <summary>
    /// Meter name for use with OpenTelemetry configuration.
    /// </summary>
    public const string MeterName = "Orbitrap.Simulator";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter for total scans received.
    /// Tags: ms_order, analyzer, polarity
    /// </summary>
    public static readonly Counter<long> ScansReceived =
        Meter.CreateCounter<long>(
            "orbitrap.scans.received",
            unit: "scans",
            description: "Total number of scans received from the instrument");

    /// <summary>
    /// Counter for scans processed by downstream consumers.
    /// Tags: ms_order, processor
    /// </summary>
    public static readonly Counter<long> ScansProcessed =
        Meter.CreateCounter<long>(
            "orbitrap.scans.processed",
            unit: "scans",
            description: "Total number of scans processed by consumers");

    /// <summary>
    /// Counter for scans dropped due to buffer overflow.
    /// Tags: reason
    /// </summary>
    public static readonly Counter<long> ScansDropped =
        Meter.CreateCounter<long>(
            "orbitrap.scans.dropped",
            unit: "scans",
            description: "Scans dropped due to buffer overflow or errors");

    /// <summary>
    /// Histogram for scan processing latency.
    /// Tags: ms_order, operation
    /// </summary>
    public static readonly Histogram<double> ScanProcessingTime =
        Meter.CreateHistogram<double>(
            "orbitrap.scan.processing_time",
            unit: "ms",
            description: "Time to process individual scans");

    /// <summary>
    /// Histogram for scan sizes (peak count).
    /// Tags: ms_order
    /// </summary>
    public static readonly Histogram<int> ScanPeakCount =
        Meter.CreateHistogram<int>(
            "orbitrap.scan.peak_count",
            unit: "peaks",
            description: "Number of peaks per scan");

    /// <summary>
    /// Histogram for total ion current values.
    /// Tags: ms_order
    /// </summary>
    public static readonly Histogram<double> ScanTIC =
        Meter.CreateHistogram<double>(
            "orbitrap.scan.tic",
            unit: "counts",
            description: "Total ion current per scan");

    /// <summary>
    /// Records metrics for a received scan.
    /// </summary>
    public static void RecordScanReceived(IOrbitrapScan scan)
    {
        var tags = new TagList
        {
            { "ms_order", scan.MsOrder },
            { "analyzer", scan.Analyzer },
            { "polarity", scan.Polarity.ToString() }
        };

        ScansReceived.Add(1, tags);
        ScanPeakCount.Record(scan.PeakCount, new TagList { { "ms_order", scan.MsOrder } });
        ScanTIC.Record(scan.TotalIonCurrent, new TagList { { "ms_order", scan.MsOrder } });
    }
}

/// <summary>
/// OpenTelemetry-compatible tracing for Orbitrap instrumentation.
/// </summary>
public static class OrbitrapTracing
{
    /// <summary>
    /// Activity source name for use with OpenTelemetry configuration.
    /// </summary>
    public const string SourceName = "Orbitrap.Simulator";

    /// <summary>
    /// Activity source for distributed tracing.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    /// <summary>
    /// Starts an activity for processing a scan.
    /// </summary>
    public static Activity? StartProcessScan(IOrbitrapScan scan, string operationName = "ProcessScan")
    {
        var activity = Source.StartActivity(operationName, ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag("scan.number", scan.ScanNumber);
            activity.SetTag("scan.ms_order", scan.MsOrder);
            activity.SetTag("scan.retention_time", scan.RetentionTime);
            activity.SetTag("scan.peak_count", scan.PeakCount);
            activity.SetTag("scan.analyzer", scan.Analyzer);
            activity.SetTag("scan.polarity", scan.Polarity.ToString());

            if (scan.MsOrder >= 2 && scan.PrecursorMass.HasValue)
            {
                activity.SetTag("scan.precursor_mz", scan.PrecursorMass.Value);
                activity.SetTag("scan.precursor_charge", scan.PrecursorCharge);
            }
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity for an acquisition session.
    /// </summary>
    public static Activity? StartAcquisition(string sessionId, string instrumentId)
    {
        var activity = Source.StartActivity("Acquisition", ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag("acquisition.session_id", sessionId);
            activity.SetTag("instrument.id", instrumentId);
        }

        return activity;
    }
}
