using FluentAssertions;
using NSubstitute;
using VirtualOrbitrap.IAPI;
using VirtualOrbitrap.Parsers;
using VirtualOrbitrap.Parsers.Dto;
using VirtualOrbitrap.Pipeline;
using VirtualOrbitrap.Schema;
using Xunit;

namespace VirtualOrbitrap.Tests.Pipeline;

public class MzMLPipelineTests
{
    [Fact]
    public async Task LoadAsync_WithMockedLoader_ShouldPopulateVirtualRawData()
    {
        // Arrange
        var mockLoader = Substitute.For<IMzMLLoader>();
        var parsedFile = CreateMockParsedFile();

        mockLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(parsedFile));

        var pipeline = new MzMLPipeline(mockLoader, new PipelineOptions { RandomSeed = 42 });

        // Act
        var rawData = await pipeline.LoadAsync("test.mzML");

        // Assert
        rawData.Should().NotBeNull();
        rawData.NumScans.Should().Be(3);
        rawData.ScanStart.Should().Be(1);
        rawData.ScanEnd.Should().Be(3);
    }

    [Fact]
    public async Task LoadAsync_ShouldPopulateCentroidStreams()
    {
        // Arrange
        var mockLoader = Substitute.For<IMzMLLoader>();
        var parsedFile = CreateMockParsedFile();

        mockLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(parsedFile));

        var pipeline = new MzMLPipeline(mockLoader, new PipelineOptions { RandomSeed = 42 });

        // Act
        var rawData = await pipeline.LoadAsync("test.mzML");

        // Assert
        var stream1 = rawData.GetCentroidStream(1);
        stream1.Should().NotBeNull();
        stream1.Masses.Should().HaveCount(3);
        stream1.Resolutions.Should().NotBeNull();
        stream1.Noises.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldPopulateScanInfo()
    {
        // Arrange
        var mockLoader = Substitute.For<IMzMLLoader>();
        var parsedFile = CreateMockParsedFile();

        mockLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(parsedFile));

        var pipeline = new MzMLPipeline(mockLoader, new PipelineOptions { RandomSeed = 42 });

        // Act
        var rawData = await pipeline.LoadAsync("test.mzML");

        // Assert
        var info1 = rawData.GetScanInfo(1);
        info1.MSLevel.Should().Be(1);
        info1.RetentionTime.Should().Be(0.5);

        var info2 = rawData.GetScanInfo(2);
        info2.MSLevel.Should().Be(2);
        info2.ParentIonMZ.Should().Be(500.0);
    }

    [Fact]
    public async Task LoadAsync_ShouldPopulateFileInfo()
    {
        // Arrange
        var mockLoader = Substitute.For<IMzMLLoader>();
        var parsedFile = CreateMockParsedFile();

        mockLoader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(parsedFile));

        var pipeline = new MzMLPipeline(mockLoader);

        // Act
        var rawData = await pipeline.LoadAsync("test.mzML");

        // Assert
        rawData.FileInfo.Should().NotBeNull();
        rawData.FileInfo.SampleName.Should().Be("test.mzML");
    }

    [Fact]
    public async Task StreamAsync_ShouldYieldConvertedScans()
    {
        // Arrange
        var mockLoader = Substitute.For<IMzMLLoader>();
        var scans = CreateMockScans();

        mockLoader.StreamScansAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(scans.ToAsyncEnumerable());

        var pipeline = new MzMLPipeline(mockLoader, new PipelineOptions
        {
            RandomSeed = 42,
            ReplayMode = ReplayMode.Immediate
        });

        // Act
        var results = new List<(CentroidStream, ScanInfo)>();
        await foreach (var item in pipeline.StreamAsync("test.mzML"))
        {
            results.Add(item);
        }

        // Assert
        results.Should().HaveCount(3);
        results[0].Item1.ScanNumber.Should().Be(1);
        results[0].Item2.MSLevel.Should().Be(1);
    }

    [Fact]
    public async Task StreamWithEventsAsync_ShouldFireScanArrivedEvents()
    {
        // Arrange
        var mockLoader = Substitute.For<IMzMLLoader>();
        var scans = CreateMockScans();

        mockLoader.StreamScansAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(scans.ToAsyncEnumerable());

        var pipeline = new MzMLPipeline(mockLoader, new PipelineOptions
        {
            RandomSeed = 42,
            ReplayMode = ReplayMode.Immediate
        });

        var rawData = new VirtualRawData();
        var eventCount = 0;
        rawData.ScanArrived += (_, _) => eventCount++;

        // Act
        await pipeline.StreamWithEventsAsync("test.mzML", rawData);

        // Assert
        eventCount.Should().Be(3);
        rawData.NumScans.Should().Be(3);
    }

    private static ParsedRawFile CreateMockParsedFile() => new()
    {
        FilePath = "/path/to/test.mzML",
        FileName = "test.mzML",
        CreationDate = DateTime.Now,
        InstrumentModel = "Test Orbitrap",
        Scans = CreateMockScans()
    };

    private static List<ParsedScan> CreateMockScans() => new()
    {
        new ParsedScan
        {
            Index = 0,
            ScanNumber = 1,
            MsLevel = 1,
            RetentionTimeMinutes = 0.5,
            Mzs = new[] { 100.0, 200.0, 300.0 },
            Intensities = new[] { 1000.0, 2000.0, 500.0 },
            IsCentroid = true,
            Polarity = 1,
            TotalIonCurrent = 3500.0,
            BasePeakMz = 200.0,
            BasePeakIntensity = 2000.0,
            LowMz = 100.0,
            HighMz = 300.0
        },
        new ParsedScan
        {
            Index = 1,
            ScanNumber = 2,
            MsLevel = 2,
            RetentionTimeMinutes = 0.6,
            Mzs = new[] { 150.0, 250.0 },
            Intensities = new[] { 500.0, 1000.0 },
            IsCentroid = true,
            Polarity = 1,
            TotalIonCurrent = 1500.0,
            BasePeakMz = 250.0,
            BasePeakIntensity = 1000.0,
            LowMz = 150.0,
            HighMz = 250.0,
            Precursor = new PrecursorInfo
            {
                SelectedMz = 500.0,
                Charge = 2,
                ActivationMethod = "HCD",
                CollisionEnergy = 30.0
            }
        },
        new ParsedScan
        {
            Index = 2,
            ScanNumber = 3,
            MsLevel = 1,
            RetentionTimeMinutes = 1.0,
            Mzs = new[] { 100.0, 200.0, 300.0, 400.0 },
            Intensities = new[] { 800.0, 1800.0, 600.0, 400.0 },
            IsCentroid = true,
            Polarity = 1,
            TotalIonCurrent = 3600.0,
            BasePeakMz = 200.0,
            BasePeakIntensity = 1800.0,
            LowMz = 100.0,
            HighMz = 400.0
        }
    };
}

// Helper extension for async enumerable in tests
internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
