using System.Diagnostics.Metrics;

namespace Orbitrap.Console.Observability;

internal static class ConsoleMetrics
{
    internal const string MeterName = "orbitrap.console";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> ScansReceived =
        Meter.CreateCounter<long>("orbitrap.console.scans.received", unit: "{scans}", description: "Total scans received");

    private static readonly Counter<long> ScansProcessed =
        Meter.CreateCounter<long>("orbitrap.console.scans.processed", unit: "{scans}", description: "Total scans processed");

    private static readonly Histogram<double> ScanProcessingMs =
        Meter.CreateHistogram<double>("orbitrap.console.scan.processing_time_ms", unit: "ms", description: "Scan processing time");

    private static long _inflight;
    private static long _acquisitionStartTimestamp;

    private static readonly ObservableGauge<long> Inflight =
        Meter.CreateObservableGauge<long>("orbitrap.console.scan.inflight", () => Volatile.Read(ref _inflight));

    private static readonly ObservableGauge<long> AcquisitionStart =
        Meter.CreateObservableGauge<long>("orbitrap.console.acquisition.start_timestamp",
            () => Volatile.Read(ref _acquisitionStartTimestamp),
            description: "Unix timestamp when acquisition started");

    internal static void OnAcquisitionStarted() =>
        Volatile.Write(ref _acquisitionStartTimestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    internal static void OnScanReceived(int msOrder)
        => ScansReceived.Add(1, new KeyValuePair<string, object?>("ms_order", msOrder));

    internal static void OnScanProcessed(int msOrder)
        => ScansProcessed.Add(1, new KeyValuePair<string, object?>("ms_order", msOrder));

    internal static void RecordProcessingMs(int msOrder, double ms)
        => ScanProcessingMs.Record(ms, new KeyValuePair<string, object?>("ms_order", msOrder));

    internal static IDisposable TrackInflight()
    {
        Interlocked.Increment(ref _inflight);
        return new Lease();
    }

    private sealed class Lease : IDisposable
    {
        public void Dispose() => Interlocked.Decrement(ref _inflight);
    }
}
