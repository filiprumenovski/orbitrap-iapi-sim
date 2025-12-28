// ============================================================================
// VirtualOrbitrap.StreamingSimulation - Demonstrates streaming scan replay
// with events and real-time processing simulation.
// ============================================================================

using System.Diagnostics;
using VirtualOrbitrap.IAPI;
using VirtualOrbitrap.Pipeline;
using VirtualOrbitrap.Schema;

Console.WriteLine("=== Virtual Orbitrap Streaming Simulation Demo ===\n");

// Parse command line
var mzmlPath = args.Length > 0 ? args[0] : null;
var replayMode = args.Length > 1 ? ParseReplayMode(args[1]) : ReplayMode.FixedDelay;
var speedMultiplier = args.Length > 2 ? double.Parse(args[2]) : 1.0;

if (mzmlPath == null)
{
    Console.WriteLine("Usage: VirtualOrbitrap.StreamingSimulation <mzml-file> [replay-mode] [speed-multiplier]");
    Console.WriteLine();
    Console.WriteLine("Replay modes:");
    Console.WriteLine("  immediate  - No delay between scans");
    Console.WriteLine("  realtime   - Replay with original RT timing");
    Console.WriteLine("  fixed      - Fixed delay (100ms default)");
    Console.WriteLine();
    Console.WriteLine("Speed multiplier (for realtime mode):");
    Console.WriteLine("  1.0 = real-time, 0.5 = 2x speed, 2.0 = half speed");
    Console.WriteLine();
    Console.WriteLine("Running demo with synthetic streaming...\n");
    await RunSyntheticStreamDemo();
    return 0;
}

if (!File.Exists(mzmlPath))
{
    Console.WriteLine($"Error: File not found: {mzmlPath}");
    return 1;
}

await RunStreamingDemo(mzmlPath, replayMode, speedMultiplier);
return 0;

// ============================================================================
// Streaming demo with mzML file
// ============================================================================
static async Task RunStreamingDemo(string mzmlPath, ReplayMode mode, double speedMultiplier)
{
    Console.WriteLine($"File:        {Path.GetFileName(mzmlPath)}");
    Console.WriteLine($"Replay Mode: {mode}");
    Console.WriteLine($"Speed:       {speedMultiplier:F1}x");
    Console.WriteLine();

    // Configure pipeline with replay timing
    var options = new PipelineOptions
    {
        ReplayMode = mode,
        ReplayDelayMultiplier = speedMultiplier,
        FixedDelayMs = 100,
        RandomSeed = 42
    };

    var pipeline = new MzMLPipeline(options);
    var rawData = new VirtualRawData();

    // Statistics
    var scanCount = 0;
    var ms1Count = 0;
    var ms2Count = 0;
    var totalTic = 0.0;
    var sw = Stopwatch.StartNew();

    // Subscribe to scan events
    rawData.ScanArrived += (sender, args) =>
    {
        scanCount++;
        var info = rawData.GetScanInfo(args.ScanNumber);

        if (info.MSLevel == 1) ms1Count++;
        else ms2Count++;

        totalTic += info.TotalIonCurrent;

        // Print progress
        var elapsed = sw.Elapsed;
        Console.Write($"\r[{elapsed:mm\\:ss\\.ff}] Scan {args.ScanNumber,5} | MS{info.MSLevel} | RT {info.RetentionTime,7:F3} min | TIC {info.TotalIonCurrent:E2} | {GetProgressBar(scanCount)}");
    };

    Console.WriteLine("Starting replay simulation...\n");
    Console.WriteLine($"{"Time",-12} {"Scan",-6} {"MS",-4} {"RT",-10} {"TIC",-14} {"Progress"}");
    Console.WriteLine(new string('-', 70));

    // Stream with events
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("\n\nCancellation requested...");
    };

    try
    {
        await pipeline.StreamWithEventsAsync(mzmlPath, rawData, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Replay cancelled by user.");
    }

    sw.Stop();

    // Print summary
    Console.WriteLine("\n\n--- Simulation Summary ---");
    Console.WriteLine($"  Total Time:    {sw.Elapsed:mm\\:ss\\.fff}");
    Console.WriteLine($"  Total Scans:   {scanCount}");
    Console.WriteLine($"  MS1 Scans:     {ms1Count}");
    Console.WriteLine($"  MS2 Scans:     {ms2Count}");
    Console.WriteLine($"  Total TIC:     {totalTic:E2}");
    Console.WriteLine($"  Scans/sec:     {scanCount / sw.Elapsed.TotalSeconds:F1}");
}

// ============================================================================
// Synthetic streaming demo (no file required)
// ============================================================================
static async Task RunSyntheticStreamDemo()
{
    var rawData = new VirtualRawData();

    // Subscribe to events
    rawData.ScanArrived += (sender, args) =>
    {
        var info = rawData.GetScanInfo(args.ScanNumber);
        var stream = rawData.GetCentroidStream(args.ScanNumber);

        Console.WriteLine($"[Event] Scan {args.ScanNumber} arrived: MS{info.MSLevel}, {stream.Length} peaks, RT={info.RetentionTime:F3} min");
    };

    Console.WriteLine("Generating synthetic scans with events...\n");

    // Simulate async scan generation
    await GenerateAndEmitScansAsync(rawData);

    Console.WriteLine($"\n--- Final State ---");
    Console.WriteLine($"Total scans in VirtualRawData: {rawData.NumScans}");
}

static async Task GenerateAndEmitScansAsync(VirtualRawData rawData)
{
    var random = new Random(42);
    var scanNumber = 0;
    var retentionTime = 0.0;

    for (int cycle = 0; cycle < 5; cycle++)
    {
        // Generate MS1 scan
        scanNumber++;
        retentionTime += 0.5;

        var ms1Mzs = GenerateRandomMzs(random, 100, 400, 1000);
        var ms1Intensities = GenerateRandomIntensities(random, ms1Mzs.Length);

        var ms1Stream = new VirtualOrbitrap.Builders.CentroidStreamBuilder()
            .WithScanNumber(scanNumber)
            .WithPeaks(ms1Mzs, ms1Intensities)
            .Build();

        var ms1Info = new VirtualOrbitrap.Builders.ScanInfoBuilder()
            .WithScanNumber(scanNumber)
            .WithMSLevel(1)
            .WithRetentionTime(retentionTime)
            .WithPolarity(IonMode.Positive)
            .Build();

        rawData.AddScan(scanNumber, ms1Stream, ms1Info);
        await Task.Delay(100); // Simulate acquisition delay

        // Generate 2-3 MS2 scans per MS1
        var numMs2 = random.Next(2, 4);
        for (int ms2Index = 0; ms2Index < numMs2; ms2Index++)
        {
            scanNumber++;
            retentionTime += 0.02;

            // Pick a precursor from MS1
            var precursorIndex = random.Next(ms1Mzs.Length);
            var precursorMz = ms1Mzs[precursorIndex];

            var ms2Mzs = GenerateFragmentMzs(random, precursorMz);
            var ms2Intensities = GenerateRandomIntensities(random, ms2Mzs.Length);

            var ms2Stream = new VirtualOrbitrap.Builders.CentroidStreamBuilder()
                .WithScanNumber(scanNumber)
                .WithPeaks(ms2Mzs, ms2Intensities)
                .Build();

            var ms2Info = new VirtualOrbitrap.Builders.ScanInfoBuilder()
                .WithScanNumber(scanNumber)
                .WithMSLevel(2)
                .WithRetentionTime(retentionTime)
                .WithPrecursor(precursorMz, charge: 2, isolationWidth: 2.0, parentScanNumber: scanNumber - ms2Index - 1)
                .WithFragmentation(ActivationType.HCD, 30.0)
                .WithPolarity(IonMode.Positive)
                .Build();

            rawData.AddScan(scanNumber, ms2Stream, ms2Info);
            await Task.Delay(50);
        }
    }
}

static double[] GenerateRandomMzs(Random random, int minPeaks, int maxPeaks, double maxMz)
{
    var count = random.Next(minPeaks, maxPeaks);
    var mzs = new double[count];
    for (int i = 0; i < count; i++)
    {
        mzs[i] = 100 + random.NextDouble() * (maxMz - 100);
    }
    Array.Sort(mzs);
    return mzs;
}

static double[] GenerateFragmentMzs(Random random, double precursorMz)
{
    var count = random.Next(20, 50);
    var mzs = new double[count];
    var maxFragmentMz = precursorMz * 2 - 18; // Approximate maximum fragment

    for (int i = 0; i < count; i++)
    {
        mzs[i] = 100 + random.NextDouble() * (maxFragmentMz - 100);
    }
    Array.Sort(mzs);
    return mzs;
}

static double[] GenerateRandomIntensities(Random random, int count)
{
    var intensities = new double[count];
    for (int i = 0; i < count; i++)
    {
        // Log-normal distribution for realistic intensity spread
        var logIntensity = random.NextDouble() * 4 + 4; // 1e4 to 1e8
        intensities[i] = Math.Pow(10, logIntensity);
    }
    return intensities;
}

static string GetProgressBar(int count, int width = 20)
{
    var filled = Math.Min(count / 10, width);
    return $"[{new string('█', filled)}{new string('░', width - filled)}] {count}";
}

static ReplayMode ParseReplayMode(string mode) => mode.ToLowerInvariant() switch
{
    "immediate" => ReplayMode.Immediate,
    "realtime" => ReplayMode.RealTime,
    "fixed" => ReplayMode.FixedDelay,
    _ => ReplayMode.FixedDelay
};
