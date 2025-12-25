using Xunit;
using FluentAssertions;
using Orbitrap.Abstractions;

namespace Orbitrap.Abstractions.Tests;

public class ScanFilterTests
{
    [Fact]
    public void Matches_WithNoFilters_ReturnsTrue()
    {
        // Arrange
        var filter = new ScanFilter();
        var scan = CreateTestScan(msOrder: 1, retentionTime: 5.0, polarity: Polarity.Positive);

        // Act & Assert
        filter.Matches(scan).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(2, 1, false)]
    public void Matches_WithMsOrderFilter_FiltersCorrectly(int filterMsOrder, int scanMsOrder, bool expected)
    {
        // Arrange
        var filter = new ScanFilter { MsOrder = filterMsOrder };
        var scan = CreateTestScan(msOrder: scanMsOrder);

        // Act & Assert
        filter.Matches(scan).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, 15.0, 10.0, true)]   // Below max (no min)
    [InlineData(null, 5.0, 10.0, false)]   // Above max (scanRt > maxRt)
    [InlineData(5.0, null, 10.0, true)]    // Above min (no max)
    [InlineData(5.0, null, 3.0, false)]    // Below min (scanRt < minRt)
    [InlineData(5.0, 15.0, 10.0, true)]    // Within range [5, 15]
    [InlineData(5.0, 15.0, 3.0, false)]    // Below min
    [InlineData(5.0, 15.0, 20.0, false)]   // Above max
    public void Matches_WithRetentionTimeFilter_FiltersCorrectly(
        double? minRt, double? maxRt, double scanRt, bool expected)
    {
        // Arrange
        var filter = new ScanFilter
        {
            MinRetentionTime = minRt,
            MaxRetentionTime = maxRt
        };
        var scan = CreateTestScan(retentionTime: scanRt);

        // Act & Assert
        filter.Matches(scan).Should().Be(expected);
    }

    [Theory]
    [InlineData(Polarity.Positive, Polarity.Positive, true)]
    [InlineData(Polarity.Positive, Polarity.Negative, false)]
    [InlineData(Polarity.Negative, Polarity.Negative, true)]
    [InlineData(Polarity.Negative, Polarity.Positive, false)]
    public void Matches_WithPolarityFilter_FiltersCorrectly(
        Polarity filterPolarity, Polarity scanPolarity, bool expected)
    {
        // Arrange
        var filter = new ScanFilter { Polarity = filterPolarity };
        var scan = CreateTestScan(polarity: scanPolarity);

        // Act & Assert
        filter.Matches(scan).Should().Be(expected);
    }

    [Theory]
    [InlineData("Orbitrap", "Orbitrap", true)]
    [InlineData("orbitrap", "Orbitrap", true)]  // Case insensitive
    [InlineData("ORBITRAP", "Orbitrap", true)]
    [InlineData("Ion Trap", "Orbitrap", false)]
    public void Matches_WithAnalyzerFilter_FiltersCorrectly(
        string filterAnalyzer, string scanAnalyzer, bool expected)
    {
        // Arrange
        var filter = new ScanFilter { Analyzer = filterAnalyzer };
        var scan = CreateTestScan(analyzer: scanAnalyzer);

        // Act & Assert
        filter.Matches(scan).Should().Be(expected);
    }

    [Fact]
    public void Matches_WithMultipleFilters_AllMustMatch()
    {
        // Arrange
        var filter = new ScanFilter
        {
            MsOrder = 1,
            MinRetentionTime = 5.0,
            MaxRetentionTime = 15.0,
            Polarity = Polarity.Positive,
            Analyzer = "Orbitrap"
        };

        // All criteria match
        var matchingScan = CreateTestScan(
            msOrder: 1,
            retentionTime: 10.0,
            polarity: Polarity.Positive,
            analyzer: "Orbitrap");

        // One criterion doesn't match (MS order)
        var nonMatchingScan = CreateTestScan(
            msOrder: 2,
            retentionTime: 10.0,
            polarity: Polarity.Positive,
            analyzer: "Orbitrap");

        // Act & Assert
        filter.Matches(matchingScan).Should().BeTrue();
        filter.Matches(nonMatchingScan).Should().BeFalse();
    }

    [Fact]
    public void ScanFilter_IsRecord_SupportsWithExpressions()
    {
        // Arrange
        var original = new ScanFilter { MsOrder = 1 };

        // Act
        var modified = original with { MinRetentionTime = 5.0 };

        // Assert
        modified.MsOrder.Should().Be(1);
        modified.MinRetentionTime.Should().Be(5.0);
        original.MinRetentionTime.Should().BeNull(); // Original unchanged
    }

    private static FrozenOrbitrapScan CreateTestScan(
        int msOrder = 1,
        double retentionTime = 10.0,
        Polarity polarity = Polarity.Positive,
        string analyzer = "Orbitrap")
    {
        return new FrozenOrbitrapScan(
            scanNumber: 1,
            msOrder: msOrder,
            retentionTime: retentionTime,
            mzValues: new[] { 100.0 },
            intensityValues: new[] { 1000.0 },
            basePeakMz: 100.0,
            basePeakIntensity: 1000.0,
            totalIonCurrent: 1000.0,
            precursorMass: msOrder >= 2 ? 500.0 : null,
            precursorCharge: msOrder >= 2 ? 2 : null,
            precursorIntensity: null,
            isolationWidth: null,
            collisionEnergy: null,
            fragmentationType: null,
            analyzer: analyzer,
            resolutionAtMz200: 120000,
            massAccuracyPpm: 3.0,
            polarity: polarity);
    }
}
