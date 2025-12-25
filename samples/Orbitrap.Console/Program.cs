using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Orbitrap.Abstractions;
using Orbitrap.Abstractions.Diagnostics;
using Orbitrap.Integration;
using Orbitrap.Mock.Configuration;

// Build host with DI and OpenTelemetry
var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();

// Add Orbitrap instrument (mock mode for demo)
builder.Services.AddMockOrbitrapInstrument(options =>
{
    options.InstrumentName = "Demo Orbitrap Exploris 480";
    options.InstrumentId = "DEMO-001";
});

// Configure OpenTelemetry (console exporter for demo)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(OrbitrapTracing.SourceName)
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(OrbitrapMetrics.MeterName)
        .AddConsoleExporter());

var host = builder.Build();

// Get the instrument from DI
var instrument = host.Services.GetRequiredService<IOrbitrapInstrument>();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Demo");

logger.LogInformation("Connected to instrument: {Name} ({Id})",
    instrument.InstrumentName,
    instrument.InstrumentId);

Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         Orbitrap IAPI Simulator - Demo Console             ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Instrument: {instrument.InstrumentName,-42} ║");
Console.WriteLine($"║  ID: {instrument.InstrumentId,-50} ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Demo 1: Event-based API
Console.WriteLine("Demo 1: Event-based API (collecting 10 scans)");
Console.WriteLine("─────────────────────────────────────────────");

var scanCount = 0;
var cts = new CancellationTokenSource();

instrument.ScanArrived += (sender, e) =>
{
    var scan = e.Scan;
    Console.WriteLine(
        $"  [Event] Scan #{scan.ScanNumber} MS{scan.MsOrder} " +
        $"RT={scan.RetentionTime:F3}min " +
        $"Peaks={scan.PeakCount} " +
        $"TIC={scan.TotalIonCurrent:E2}");

    if (Interlocked.Increment(ref scanCount) >= 10)
    {
        cts.Cancel();
    }
};

// Start acquisition with options
var session = await instrument.StartAcquisitionAsync(new AcquisitionOptions
{
    MaxScans = 15,
    AutoFreeze = true, // Thread-safe copies
    BufferCapacity = 100
}, cts.Token);

Console.WriteLine($"  Session started: {session.SessionId}");
Console.WriteLine();

try
{
    // Wait for completion or cancellation
    await session.Completion.WaitAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Expected
}

await session.StopAsync();
await session.DisposeAsync();

Console.WriteLine();
Console.WriteLine($"  Session complete: {session.ScanCount} scans acquired");
Console.WriteLine();

// Demo 2: Async Stream API
Console.WriteLine("Demo 2: Async Stream API with filtering");
Console.WriteLine("────────────────────────────────────────");

// Create a new instrument for the second demo
var instrument2 = InstrumentFactory.CreateMockDefault();

var ms1Filter = new ScanFilter { MsOrder = 1 }; // MS1 only
var collected = new List<FrozenOrbitrapScan>();

await using (var session2 = await instrument2.StartAcquisitionAsync(new AcquisitionOptions
{
    MaxScans = 20
}))
{
    await foreach (var scan in session2.Scans)
    {
        if (ms1Filter.Matches(scan))
        {
            var frozen = scan.ToFrozen();
            collected.Add(frozen);

            Console.WriteLine(
                $"  [Stream] MS1 #{frozen.ScanNumber} " +
                $"RT={frozen.RetentionTime:F3}min " +
                $"BasePeak={frozen.BasePeakMz:F4} m/z @ {frozen.BasePeakIntensity:E2}");
        }

        if (collected.Count >= 5)
        {
            await session2.StopAsync();
            break;
        }
    }
}

Console.WriteLine();
Console.WriteLine($"  Collected {collected.Count} MS1 scans");

// Show statistics
if (collected.Count > 0)
{
    var avgPeaks = collected.Average(s => s.PeakCount);
    var avgTIC = collected.Average(s => s.TotalIonCurrent);

    Console.WriteLine();
    Console.WriteLine("Statistics:");
    Console.WriteLine($"  Average peaks per scan: {avgPeaks:F0}");
    Console.WriteLine($"  Average TIC: {avgTIC:E2}");
}

await instrument2.DisposeAsync();

Console.WriteLine();
Console.WriteLine("Demo 3: Direct iteration (LINQ-compatible)");
Console.WriteLine("──────────────────────────────────────────");

var instrument3 = InstrumentFactory.CreateMockDefault();

var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(3));

try
{
    var highTICScans = await instrument3
        .GetScansAsync(cancellationToken: cts3.Token)
        .Where(s => s.TotalIonCurrent > 1e7)
        .Take(3)
        .ToListAsync(cts3.Token);

    foreach (var scan in highTICScans)
    {
        Console.WriteLine(
            $"  High TIC scan #{scan.ScanNumber}: TIC={scan.TotalIonCurrent:E2}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("  (Timed out - instrument not actively acquiring)");
}

await instrument3.DisposeAsync();

Console.WriteLine();
Console.WriteLine("════════════════════════════════════════════════════════════");
Console.WriteLine("Demo complete! Press any key to exit.");
Console.ReadKey();
