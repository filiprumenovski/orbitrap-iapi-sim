using Xunit;
using FluentAssertions;
using Orbitrap.Abstractions;

namespace Orbitrap.Abstractions.Tests;

public class OrbitrapScanEventArgsTests
{
    [Fact]
    public void Constructor_WithValidScan_SetsScanAndTimestamp()
    {
        // Arrange
        var scan = CreateTestScan();
        var before = DateTimeOffset.UtcNow;

        // Act
        var args = new OrbitrapScanEventArgs(scan);
        var after = DateTimeOffset.UtcNow;

        // Assert
        args.Scan.Should().BeSameAs(scan);
        args.Timestamp.Should().BeOnOrAfter(before);
        args.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_WithNullScan_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new OrbitrapScanEventArgs(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("scan");
    }

    [Fact]
    public void Timestamp_IsUtc()
    {
        // Arrange
        var scan = CreateTestScan();

        // Act
        var args = new OrbitrapScanEventArgs(scan);

        // Assert
        args.Timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    private static FrozenOrbitrapScan CreateTestScan()
    {
        return new FrozenOrbitrapScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 0,
            mzValues: Array.Empty<double>(),
            intensityValues: Array.Empty<double>(),
            basePeakMz: 0,
            basePeakIntensity: 0,
            totalIonCurrent: 0,
            precursorMass: null,
            precursorCharge: null,
            precursorIntensity: null,
            isolationWidth: null,
            collisionEnergy: null,
            fragmentationType: null,
            analyzer: "Orbitrap",
            resolutionAtMz200: 120000,
            massAccuracyPpm: 3.0,
            polarity: Polarity.Positive);
    }
}
