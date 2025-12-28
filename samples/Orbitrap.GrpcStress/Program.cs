using System.Diagnostics;
using System.Diagnostics.Metrics;
using Grpc.Net.Client;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Orbitrap.Abstractions.Diagnostics;
using Orbitrap.Simulator.Grpc;

static string GetArg(string[] args, string name, string defaultValue)
{
    var idx = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (idx < 0 || idx == args.Length - 1)
    {
        return defaultValue;
    }

    return args[idx + 1];
}

static int GetIntArg(string[] args, string name, int defaultValue)
    => int.TryParse(GetArg(args, name, defaultValue.ToString()), out var v) ? v : defaultValue;

static double GetDoubleArg(string[] args, string name, double defaultValue)
    => double.TryParse(GetArg(args, name, defaultValue.ToString()), out var v) ? v : defaultValue;

static bool HasFlag(string[] args, string flag)
    => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

// OpenTelemetry metrics setup
var metricsPort = GetIntArg(args, "--metrics-port", 9465);
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(OrbitrapMetrics.MeterName)
    .AddMeter(StressMetrics.MeterName)
    // Bind to all interfaces so Prometheus can scrape from Docker network
    .AddPrometheusHttpListener(options => options.UriPrefixes = new[]
    {
        $"http://*:{metricsPort}/",
    })
    .Build();

var endpoint = GetArg(args, "--endpoint", "http://localhost:31417");
var durationSeconds = GetDoubleArg(args, "--duration", 10);
var warmupSeconds = GetDoubleArg(args, "--warmup", 1);
var scanRate = GetDoubleArg(args, "--scan-rate", 10_000);
var ms2PerMs1 = GetIntArg(args, "--ms2-per-ms1", 0);
var ms1Peaks = GetIntArg(args, "--ms1-peaks", 10);
var ms2Peaks = GetIntArg(args, "--ms2-peaks", 10);
var bufferSize = GetIntArg(args, "--buffer-size", 50_000);
var quiet = HasFlag(args, "--quiet");

Console.WriteLine("=== Orbitrap gRPC Stress Test ===");
Console.WriteLine($"Endpoint:     {endpoint}");
Console.WriteLine($"Warmup:       {warmupSeconds:F1}s");
Console.WriteLine($"Measure:      {durationSeconds:F1}s");
Console.WriteLine($"Scan rate:    {scanRate:F0} (server cycles/sec)");
Console.WriteLine($"MS2 per MS1:  {ms2PerMs1}");
Console.WriteLine($"MS1 peaks:    {ms1Peaks}");
Console.WriteLine($"MS2 peaks:    {ms2Peaks}");
Console.WriteLine($"Buffer size:  {bufferSize}");
Console.WriteLine($"Metrics:      http://localhost:{metricsPort}/metrics (and scrapeable from Docker via host.docker.internal)");
Console.WriteLine();

var channel = GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
{
    MaxReceiveMessageSize = 128 * 1024 * 1024,
});

var client = new SimulatorService.SimulatorServiceClient(channel);

var totalRunSeconds = warmupSeconds + durationSeconds;

var start = await client.StartAcquisitionAsync(new StartAcquisitionRequest
{
    MaxDurationSeconds = totalRunSeconds,
    Simulation = new SimulationParameters
    {
        ScanRate = scanRate,
        Ms2PerMs1 = ms2PerMs1,
        MinMz = 200,
        MaxMz = 2000,
        Resolution = 120000,
        NoiseLevel = 0.01,
        RandomSeed = 42,
        Ms1PeakCount = ms1Peaks,
        Ms2PeakCount = ms2Peaks,
    }
});

if (!start.Success)
{
    Console.Error.WriteLine($"StartAcquisition failed: {start.ErrorMessage}");
    return 2;
}

Console.WriteLine($"Session: {start.SessionId}");
Console.WriteLine();

// The server stops generating at MaxDurationSeconds, but StreamScans is an open-ended broadcast
// stream. Use time-based cancellation so we don't hang once scans stop.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(totalRunSeconds + 0.5));

var call = client.StreamScans(new StreamScansRequest
{
    BufferSize = bufferSize,
}, cancellationToken: cts.Token);

var swTotal = Stopwatch.StartNew();
var swMeasure = Stopwatch.StartNew();

long totalScans = 0;
long measuredScans = 0;
long totalPeaks = 0;

var lastReport = TimeSpan.Zero;
try
{
    while (await call.ResponseStream.MoveNext(cts.Token))
    {
        var scan = call.ResponseStream.Current;

        totalScans++;
        totalPeaks += scan.MzValues.Count;

        // Record metrics for dashboard
        StressMetrics.OnScanReceived(scan.MsOrder, scan.MzValues.Count, scan.TotalIonCurrent);

        var elapsed = swTotal.Elapsed;

        if (elapsed.TotalSeconds >= warmupSeconds)
        {
            measuredScans++;
        }

        if (!quiet && elapsed - lastReport >= TimeSpan.FromSeconds(1))
        {
            lastReport = elapsed;
            var measuredSeconds = Math.Max(0.001, elapsed.TotalSeconds - warmupSeconds);
            var currentRate = measuredScans / measuredSeconds;
            Console.WriteLine($"t={elapsed.TotalSeconds,6:F1}s scans={totalScans,10} measured={measuredScans,10} rate={currentRate,10:F0} scans/s");
        }

        if (elapsed.TotalSeconds >= totalRunSeconds)
        {
            cts.Cancel();
            break;
        }
    }
}
catch (OperationCanceledException)
{
    // Expected when we end the measurement window.
}
catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
{
    // Expected when we end the measurement window.
}

swTotal.Stop();

var achieved = measuredScans / Math.Max(0.001, durationSeconds);
var avgPeaks = totalScans > 0 ? (double)totalPeaks / totalScans : 0;

try
{
    await client.StopAcquisitionAsync(new StopAcquisitionRequest { SessionId = start.SessionId });
}
catch
{
    // Best-effort.
}

Console.WriteLine();
Console.WriteLine("--- Summary ---");
Console.WriteLine($"Total scans received:     {totalScans}");
Console.WriteLine($"Measured scans received:  {measuredScans}");
Console.WriteLine($"Measured duration:        {durationSeconds:F3}s");
Console.WriteLine($"Achieved rate:            {achieved:F0} scans/s");
Console.WriteLine($"Average peaks/scan:       {avgPeaks:F1}");

// Exit non-zero if explicitly targeting 10k+ and we missed.
var target = GetDoubleArg(args, "--target", 10_000);
if (achieved < target)
{
    Console.Error.WriteLine($"FAIL: achieved {achieved:F0} < target {target:F0} scans/s");
    return 1;
}

Console.WriteLine($"PASS: achieved {achieved:F0} >= target {target:F0} scans/s");
return 0;

/// <summary>
/// Metrics for the stress test client, compatible with OrbitrapMetrics for unified dashboard.
/// </summary>
internal static class StressMetrics
{
    internal const string MeterName = "orbitrap.stress";
    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> ScansReceived =
        Meter.CreateCounter<long>("orbitrap.stress.scans.received", description: "Total scans received");

    private static readonly Counter<long> ScansProcessed =
        Meter.CreateCounter<long>("orbitrap.stress.scans.processed", description: "Total scans processed");

    private static readonly Histogram<int> ScanPeakCount =
        Meter.CreateHistogram<int>("orbitrap.stress.scan.peak_count", unit: "peaks", description: "Peaks per scan");

    private static readonly Histogram<double> ScanTIC =
        Meter.CreateHistogram<double>("orbitrap.stress.scan.tic", unit: "counts", description: "Total ion current per scan");

    internal static void OnScanReceived(int msOrder, int peakCount, double tic)
    {
        var tags = new TagList { { "ms_order", msOrder } };
        ScansReceived.Add(1, tags);
        ScansProcessed.Add(1, tags);
        ScanPeakCount.Record(peakCount, tags);
        ScanTIC.Record(tic, tags);
    }
}
