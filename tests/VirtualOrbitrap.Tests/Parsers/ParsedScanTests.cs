using FluentAssertions;
using VirtualOrbitrap.Parsers.Dto;
using Xunit;

namespace VirtualOrbitrap.Tests.Parsers;

public class ParsedScanTests
{
    [Fact]
    public void ParsedScan_DefaultValues_ShouldBeEmpty()
    {
        // Arrange & Act
        var scan = new ParsedScan();

        // Assert
        scan.Index.Should().Be(0);
        scan.ScanNumber.Should().Be(0);
        scan.MsLevel.Should().Be(0);
        scan.RetentionTimeMinutes.Should().Be(0);
        scan.Mzs.Should().BeEmpty();
        scan.Intensities.Should().BeEmpty();
        scan.PeakCount.Should().Be(0);
        scan.Precursor.Should().BeNull();
    }

    [Fact]
    public void ParsedScan_WithPeaks_ShouldReturnCorrectPeakCount()
    {
        // Arrange
        var mzs = new double[] { 100.0, 200.0, 300.0 };
        var intensities = new double[] { 1000.0, 2000.0, 500.0 };

        // Act
        var scan = new ParsedScan
        {
            Mzs = mzs,
            Intensities = intensities
        };

        // Assert
        scan.PeakCount.Should().Be(3);
    }

    [Fact]
    public void ParsedScan_MS2WithPrecursor_ShouldHavePrecursorInfo()
    {
        // Arrange & Act
        var scan = new ParsedScan
        {
            ScanNumber = 5,
            MsLevel = 2,
            RetentionTimeMinutes = 1.5,
            Precursor = new PrecursorInfo
            {
                SelectedMz = 750.5,
                Charge = 2,
                ActivationMethod = "HCD",
                CollisionEnergy = 30.0
            }
        };

        // Assert
        scan.MsLevel.Should().Be(2);
        scan.Precursor.Should().NotBeNull();
        scan.Precursor!.SelectedMz.Should().Be(750.5);
        scan.Precursor.Charge.Should().Be(2);
        scan.Precursor.ActivationMethod.Should().Be("HCD");
    }
}
