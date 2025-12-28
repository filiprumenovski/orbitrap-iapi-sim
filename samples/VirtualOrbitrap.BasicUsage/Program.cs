// ============================================================================
// VirtualOrbitrap.BasicUsage - Demonstrates loading mzML files and accessing
// scan data through the Virtual Orbitrap IAPI interface.
// ============================================================================

using VirtualOrbitrap.IAPI;
using VirtualOrbitrap.Pipeline;
using VirtualOrbitrap.Schema;

Console.WriteLine("=== Virtual Orbitrap Basic Usage Demo ===\n");

// Check command line arguments
if (args.Length == 0)
{
    Console.WriteLine("Usage: VirtualOrbitrap.BasicUsage <path-to-mzml-file>");
    Console.WriteLine("\nRunning demo with synthetic data...\n");
    await RunSyntheticDemo();
}
else
{
    var mzmlPath = args[0];
    if (!File.Exists(mzmlPath))
    {
        Console.WriteLine($"Error: File not found: {mzmlPath}");
        return 1;
    }
    await RunMzMLDemo(mzmlPath);
}

return 0;

// ============================================================================
// Demo with mzML file
// ============================================================================
static async Task RunMzMLDemo(string mzmlPath)
{
    Console.WriteLine($"Loading mzML file: {mzmlPath}\n");

    // Configure the pipeline
    var options = new PipelineOptions
    {
        ResolutionR0 = 60000,      // Resolution at 200 m/z
        SynthesizeNoise = true,    // Add synthetic noise
        SynthesizeBaseline = true, // Add synthetic baseline
        GenerateFilterStrings = true
    };

    // Create pipeline and load file
    var pipeline = new MzMLPipeline(options);
    var rawData = await pipeline.LoadAsync(mzmlPath);

    // Display file metadata
    PrintFileInfo(rawData);

    // Display first few scans
    PrintScanSummary(rawData, maxScans: 10);

    // Example: Access specific scan data
    if (rawData.NumScans > 0)
    {
        PrintDetailedScan(rawData, rawData.ScanStart);
    }
}

// ============================================================================
// Demo with synthetic data (no mzML file required)
// ============================================================================
static async Task RunSyntheticDemo()
{
    // Create a VirtualRawData instance manually
    var rawData = CreateSyntheticData();

    // Display file metadata
    PrintFileInfo(rawData);

    // Display all scans
    PrintScanSummary(rawData, maxScans: 10);

    // Display detailed first scan
    PrintDetailedScan(rawData, 1);

    await Task.CompletedTask;
}

static VirtualRawData CreateSyntheticData()
{
    var fileInfo = new VirtualOrbitrap.Builders.RawFileInfoBuilder()
        .WithSampleName("Synthetic Sample")
        .WithInstrument("Virtual Orbitrap", "Simulator v1.0", "SIM-001")
        .WithResolution(60000)
        .WithCreationDate(DateTime.Now)
        .WithComment("Synthetic demo data")
        .Build();

    var rawData = new VirtualRawData(fileInfo);

    // Add MS1 scan
    var ms1Stream = new VirtualOrbitrap.Builders.CentroidStreamBuilder()
        .WithScanNumber(1)
        .WithPeaks(
            new double[] { 400.2, 500.3, 600.4, 700.5, 800.6 },
            new double[] { 1e6, 5e6, 2e6, 3e6, 1.5e6 })
        .Build();

    var ms1Info = new VirtualOrbitrap.Builders.ScanInfoBuilder()
        .WithScanNumber(1)
        .WithMSLevel(1)
        .WithRetentionTime(0.5)
        .WithPolarity(IonMode.Positive)
        .AsCentroid()
        .Build();

    rawData.AddScan(1, ms1Stream, ms1Info);

    // Add MS2 scan
    var ms2Stream = new VirtualOrbitrap.Builders.CentroidStreamBuilder()
        .WithScanNumber(2)
        .WithPeaks(
            new double[] { 175.1, 250.2, 350.3, 450.4 },
            new double[] { 5e5, 2e6, 8e5, 1e6 })
        .Build();

    var ms2Info = new VirtualOrbitrap.Builders.ScanInfoBuilder()
        .WithScanNumber(2)
        .WithMSLevel(2)
        .WithRetentionTime(0.52)
        .WithPrecursor(500.3, charge: 2, isolationWidth: 2.0, parentScanNumber: 1)
        .WithFragmentation(ActivationType.HCD, 30.0)
        .WithPolarity(IonMode.Positive)
        .AsCentroid()
        .Build();

    rawData.AddScan(2, ms2Stream, ms2Info);

    // Add another MS1 scan
    var ms1Stream2 = new VirtualOrbitrap.Builders.CentroidStreamBuilder()
        .WithScanNumber(3)
        .WithPeaks(
            new double[] { 401.2, 501.3, 601.4, 701.5, 801.6 },
            new double[] { 1.1e6, 4.8e6, 2.2e6, 2.9e6, 1.6e6 })
        .Build();

    var ms1Info2 = new VirtualOrbitrap.Builders.ScanInfoBuilder()
        .WithScanNumber(3)
        .WithMSLevel(1)
        .WithRetentionTime(1.0)
        .WithPolarity(IonMode.Positive)
        .AsCentroid()
        .Build();

    rawData.AddScan(3, ms1Stream2, ms1Info2);

    return rawData;
}

// ============================================================================
// Helper methods for display
// ============================================================================
static void PrintFileInfo(VirtualRawData rawData)
{
    Console.WriteLine("--- File Information ---");
    Console.WriteLine($"  Sample Name:    {rawData.FileInfo.SampleName}");
    Console.WriteLine($"  Instrument:     {rawData.FileInfo.InstModel}");
    Console.WriteLine($"  Serial Number:  {rawData.FileInfo.InstSerialNumber}");
    Console.WriteLine($"  Creation Date:  {rawData.FileInfo.CreationDate}");
    Console.WriteLine($"  Total Scans:    {rawData.NumScans}");
    Console.WriteLine($"  Scan Range:     {rawData.ScanStart} - {rawData.ScanEnd}");
    Console.WriteLine($"  Mass Resolution: {rawData.FileInfo.MassResolution:N0}");
    Console.WriteLine();
}

static void PrintScanSummary(VirtualRawData rawData, int maxScans)
{
    Console.WriteLine("--- Scan Summary ---");
    Console.WriteLine($"{"Scan",-6} {"MS",-4} {"RT (min)",-10} {"TIC",-12} {"Base Peak",-12} {"Filter"}");
    Console.WriteLine(new string('-', 80));

    var count = 0;
    for (int scan = rawData.ScanStart; scan <= rawData.ScanEnd && count < maxScans; scan++, count++)
    {
        var info = rawData.GetScanInfo(scan);
        Console.WriteLine($"{scan,-6} {info.MSLevel,-4} {info.RetentionTime,-10:F3} {info.TotalIonCurrent,-12:E2} {info.BasePeakMZ,-12:F4} {TruncateFilter(info.FilterText, 30)}");
    }

    if (rawData.NumScans > maxScans)
    {
        Console.WriteLine($"... and {rawData.NumScans - maxScans} more scans");
    }
    Console.WriteLine();
}

static void PrintDetailedScan(VirtualRawData rawData, int scanNumber)
{
    Console.WriteLine($"--- Detailed Scan {scanNumber} ---");

    var info = rawData.GetScanInfo(scanNumber);
    var stream = rawData.GetCentroidStream(scanNumber);
    var stats = rawData.GetScanStatistics(scanNumber);

    Console.WriteLine($"  MS Level:         {info.MSLevel}");
    Console.WriteLine($"  Retention Time:   {info.RetentionTime:F4} min");
    Console.WriteLine($"  Ion Mode:         {info.IonMode}");
    Console.WriteLine($"  Is Centroided:    {info.IsCentroided}");
    Console.WriteLine($"  Filter Text:      {info.FilterText}");
    Console.WriteLine();

    if (info.MSLevel > 1)
    {
        Console.WriteLine($"  Precursor m/z:    {info.ParentIonMZ:F4}");
        Console.WriteLine($"  Isolation Width:  {info.IsolationWindowWidthMZ:F2}");
        Console.WriteLine($"  Activation:       {info.ActivationType}");
        Console.WriteLine();
    }

    Console.WriteLine($"  Number of Peaks:  {stream.Length}");
    Console.WriteLine($"  TIC:              {stats.TIC:E2}");
    Console.WriteLine($"  Base Peak m/z:    {stats.BasePeakMass:F4}");
    Console.WriteLine($"  Base Peak Int:    {stats.BasePeakIntensity:E2}");
    Console.WriteLine();

    // Show top 5 peaks
    Console.WriteLine("  Top 5 Peaks:");
    Console.WriteLine($"  {"m/z",-14} {"Intensity",-14} {"Resolution",-12} {"S/N"}");

    var peakOrder = stream.Intensities
        .Select((intensity, index) => (intensity, index))
        .OrderByDescending(x => x.intensity)
        .Take(5);

    foreach (var (intensity, i) in peakOrder)
    {
        var mz = stream.Masses[i];
        var resolution = stream.Resolutions?[i] ?? 0;
        var noise = stream.Noises?[i] ?? 1;
        var sn = intensity / noise;
        Console.WriteLine($"  {mz,-14:F6} {intensity,-14:E2} {resolution,-12:N0} {sn:F1}");
    }
    Console.WriteLine();
}

static string TruncateFilter(string filter, int maxLength)
{
    if (string.IsNullOrEmpty(filter)) return "";
    return filter.Length <= maxLength ? filter : filter[..(maxLength - 3)] + "...";
}
