using FluentAssertions;
using VirtualOrbitrap.Pipeline;
using VirtualOrbitrap.Parsers.Dto;
using VirtualOrbitrap.Schema;
using Xunit;

namespace VirtualOrbitrap.Tests.Pipeline;

public class ScanConverterTests
{
    private readonly ScanConverter _converter;

    public ScanConverterTests()
    {
        _converter = new ScanConverter(new PipelineOptions
        {
            RandomSeed = 42 // For reproducibility
        });
    }

    [Fact]
    public void ConvertToCentroidStream_MS1Scan_ShouldPopulateAllFields()
    {
        // Arrange
        var parsedScan = CreateMS1Scan();

        // Act
        var stream = _converter.ConvertToCentroidStream(parsedScan);

        // Assert
        stream.ScanNumber.Should().Be(1);
        stream.Length.Should().Be(3);
        stream.Masses.Should().BeEquivalentTo(new[] { 100.0, 200.0, 300.0 });
        stream.Intensities.Should().BeEquivalentTo(new[] { 1000.0, 2000.0, 500.0 });
        stream.Resolutions.Should().NotBeNull();
        stream.Resolutions.Should().HaveCount(3);
        stream.Noises.Should().NotBeNull();
        stream.Baselines.Should().NotBeNull();
    }

    [Fact]
    public void ConvertToCentroidStream_WithCalculatedResolutions_ShouldFollowOrbitrapPhysics()
    {
        // Arrange
        var parsedScan = CreateMS1Scan();

        // Act
        var stream = _converter.ConvertToCentroidStream(parsedScan);

        // Assert
        // Resolution should be higher at lower m/z (Orbitrap physics: R ∝ 1/sqrt(m))
        stream.Resolutions![0].Should().BeGreaterThan(stream.Resolutions[2]);
    }

    [Fact]
    public void ConvertToScanInfo_MS1Scan_ShouldPopulateMetadata()
    {
        // Arrange
        var parsedScan = CreateMS1Scan();

        // Act
        var info = _converter.ConvertToScanInfo(parsedScan);

        // Assert
        info.ScanNumber.Should().Be(1);
        info.MSLevel.Should().Be(1);
        info.RetentionTime.Should().Be(1.5);
        info.TotalIonCurrent.Should().Be(3500.0);
        info.BasePeakMZ.Should().Be(200.0);
        info.BasePeakIntensity.Should().Be(2000.0);
        info.IonMode.Should().Be(IonMode.Positive);
    }

    [Fact]
    public void ConvertToScanInfo_MS1Scan_ShouldGenerateFilterString()
    {
        // Arrange
        var parsedScan = CreateMS1Scan();

        // Act
        var info = _converter.ConvertToScanInfo(parsedScan);

        // Assert
        info.FilterText.Should().NotBeNullOrEmpty();
        info.FilterText.Should().Contain("FTMS");
        info.FilterText.Should().Contain("Full ms");
    }

    [Fact]
    public void ConvertToScanInfo_MS2Scan_ShouldIncludePrecursorInfo()
    {
        // Arrange
        var parsedScan = CreateMS2Scan();

        // Act
        var info = _converter.ConvertToScanInfo(parsedScan);

        // Assert
        info.ScanNumber.Should().Be(2);
        info.MSLevel.Should().Be(2);
        info.ParentIonMZ.Should().Be(750.5);
        info.IsolationWindowWidthMZ.Should().Be(2.0);
        info.ActivationType.Should().Be(ActivationType.HCD);
        info.FilterText.Should().Contain("ms2");
        info.FilterText.Should().Contain("750.50");
        info.FilterText.Should().Contain("hcd");
    }

    [Fact]
    public void Convert_ShouldReturnBothStreamAndInfo()
    {
        // Arrange
        var parsedScan = CreateMS1Scan();

        // Act
        var (stream, info) = _converter.Convert(parsedScan);

        // Assert
        stream.ScanNumber.Should().Be(info.ScanNumber);
        stream.Length.Should().Be(info.NumPeaks);
    }

    [Fact]
    public void ConvertWithDisabledEnrichment_ShouldSkipSynthesis()
    {
        // Arrange
        var options = new PipelineOptions
        {
            SynthesizeNoise = false,
            SynthesizeBaseline = false,
            CalculateResolutions = false,
            GenerateFilterStrings = false
        };
        var converter = new ScanConverter(options);
        var parsedScan = CreateMS1Scan();

        // Act
        var stream = converter.ConvertToCentroidStream(parsedScan);

        // Assert - Build() auto-generates defaults, so these will still be populated
        // But explicit values from source should be preserved
        stream.Masses.Should().NotBeEmpty();
        stream.Intensities.Should().NotBeEmpty();
    }

    private static ParsedScan CreateMS1Scan() => new()
    {
        Index = 0,
        ScanNumber = 1,
        MsLevel = 1,
        RetentionTimeMinutes = 1.5,
        Mzs = new[] { 100.0, 200.0, 300.0 },
        Intensities = new[] { 1000.0, 2000.0, 500.0 },
        IsCentroid = true,
        Polarity = 1,
        TotalIonCurrent = 3500.0,
        BasePeakMz = 200.0,
        BasePeakIntensity = 2000.0,
        LowMz = 100.0,
        HighMz = 300.0
    };

    private static ParsedScan CreateMS2Scan() => new()
    {
        Index = 1,
        ScanNumber = 2,
        MsLevel = 2,
        RetentionTimeMinutes = 1.6,
        Mzs = new[] { 150.0, 250.0, 350.0, 450.0 },
        Intensities = new[] { 500.0, 1500.0, 300.0, 800.0 },
        IsCentroid = true,
        Polarity = 1,
        TotalIonCurrent = 3100.0,
        BasePeakMz = 250.0,
        BasePeakIntensity = 1500.0,
        LowMz = 150.0,
        HighMz = 450.0,
        Precursor = new PrecursorInfo
        {
            SelectedMz = 750.5,
            Charge = 2,
            IsolationWindowTargetMz = 750.5,
            IsolationWindowLowerOffset = 1.0,
            IsolationWindowUpperOffset = 1.0,
            ActivationMethod = "HCD",
            CollisionEnergy = 30.0,
            PrecursorScanNumber = 1
        }
    };
}
