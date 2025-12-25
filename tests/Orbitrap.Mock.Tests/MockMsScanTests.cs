using Xunit;
using FluentAssertions;
using Orbitrap.Abstractions;
using Orbitrap.Mock;

namespace Orbitrap.Mock.Tests;

public class MockMsScanTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var scan = new MockMsScan(
            scanNumber: 42,
            msOrder: 2,
            retentionTime: 15.5,
            mzValues: new[] { 100.0, 200.0, 300.0 },
            intensityValues: new[] { 1000.0, 5000.0, 2000.0 },
            basePeakMz: 200.0,
            basePeakIntensity: 5000.0,
            totalIonCurrent: 8000.0,
            precursorMass: 500.5,
            precursorCharge: 2,
            precursorIntensity: 10000.0,
            isolationWidth: 1.6,
            collisionEnergy: 30.0,
            fragmentationType: FragmentationType.HCD,
            analyzer: "Orbitrap",
            resolutionAtMz200: 120000,
            massAccuracyPpm: 3.0,
            polarity: Polarity.Positive);

        // Assert
        scan.ScanNumber.Should().Be(42);
        scan.MsOrder.Should().Be(2);
        scan.RetentionTime.Should().Be(15.5);
        scan.PeakCount.Should().Be(3);
        scan.BasePeakMz.Should().Be(200.0);
        scan.BasePeakIntensity.Should().Be(5000.0);
        scan.TotalIonCurrent.Should().Be(8000.0);
        scan.PrecursorMass.Should().Be(500.5);
        scan.PrecursorCharge.Should().Be(2);
        scan.PrecursorIntensity.Should().Be(10000.0);
        scan.IsolationWidth.Should().Be(1.6);
        scan.CollisionEnergy.Should().Be(30.0);
        scan.FragmentationType.Should().Be(FragmentationType.HCD);
        scan.Analyzer.Should().Be("Orbitrap");
        scan.ResolutionAtMz200.Should().Be(120000);
        scan.MassAccuracyPpm.Should().Be(3.0);
        scan.Polarity.Should().Be(Polarity.Positive);
    }

    [Fact]
    public void Constructor_WithDefaultOptionalParameters_SetsDefaults()
    {
        // Act
        var scan = new MockMsScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 0,
            mzValues: Array.Empty<double>(),
            intensityValues: Array.Empty<double>(),
            basePeakMz: 0,
            basePeakIntensity: 0,
            totalIonCurrent: 0);

        // Assert
        scan.PrecursorMass.Should().BeNull();
        scan.PrecursorCharge.Should().BeNull();
        scan.FragmentationType.Should().BeNull();
        scan.Analyzer.Should().Be("Orbitrap");
        scan.ResolutionAtMz200.Should().Be(120000);
        scan.MassAccuracyPpm.Should().Be(3.0);
        scan.Polarity.Should().Be(Polarity.Positive);
    }

    [Fact]
    public void MzValues_ReturnsReadOnlyMemory()
    {
        // Arrange
        var mzValues = new[] { 100.0, 200.0, 300.0 };
        var scan = CreateScanWithSpectrum(mzValues, new double[3]);

        // Act
        var memory = scan.MzValues;

        // Assert
        memory.Length.Should().Be(3);
        memory.Span[0].Should().Be(100.0);
        memory.Span[1].Should().Be(200.0);
        memory.Span[2].Should().Be(300.0);
    }

    [Fact]
    public void ToFrozen_CreatesFrozenCopy()
    {
        // Arrange
        var scan = CreateMs1Scan();

        // Act
        var frozen = scan.ToFrozen();

        // Assert
        frozen.Should().NotBeNull();
        frozen.Should().BeOfType<FrozenOrbitrapScan>();
        frozen.ScanNumber.Should().Be(scan.ScanNumber);
        frozen.MsOrder.Should().Be(scan.MsOrder);
        frozen.MzValues.ToArray().Should().BeEquivalentTo(scan.MzValues.ToArray());
    }

    [Fact]
    public void Dispose_ReleasesArrayPoolMemory()
    {
        // Arrange
        var scan = new MockMsScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 0,
            mzValues: new double[100],
            intensityValues: new double[100],
            basePeakMz: 0,
            basePeakIntensity: 0,
            totalIonCurrent: 0,
            useArrayPool: true);

        // Act & Assert - should not throw
        scan.Dispose();
        scan.Dispose(); // Second dispose should be safe
    }

    [Fact]
    public void Constructor_WithNullArrays_CreatesEmptyScan()
    {
        // Act
        var scan = new MockMsScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 0,
            mzValues: null!,
            intensityValues: null!,
            basePeakMz: 0,
            basePeakIntensity: 0,
            totalIonCurrent: 0);

        // Assert
        scan.MzValues.Length.Should().Be(0);
        scan.IntensityValues.Length.Should().Be(0);
        scan.PeakCount.Should().Be(0);
    }

    private static MockMsScan CreateMs1Scan()
    {
        return new MockMsScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 1.5,
            mzValues: new[] { 100.0, 200.0, 300.0 },
            intensityValues: new[] { 1000.0, 5000.0, 2000.0 },
            basePeakMz: 200.0,
            basePeakIntensity: 5000.0,
            totalIonCurrent: 8000.0);
    }

    private static MockMsScan CreateScanWithSpectrum(double[] mzValues, double[] intensityValues)
    {
        return new MockMsScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 0,
            mzValues: mzValues,
            intensityValues: intensityValues,
            basePeakMz: mzValues.Length > 0 ? mzValues[0] : 0,
            basePeakIntensity: intensityValues.Length > 0 ? intensityValues[0] : 0,
            totalIonCurrent: intensityValues.Sum());
    }
}

public class MockMsScanBuilderTests
{
    [Fact]
    public void Build_WithDefaults_CreatesValidScan()
    {
        // Act
        var scan = new MockMsScanBuilder().Build();

        // Assert
        scan.ScanNumber.Should().Be(1);
        scan.MsOrder.Should().Be(1);
        scan.RetentionTime.Should().Be(0);
        scan.Analyzer.Should().Be("Orbitrap");
        scan.Polarity.Should().Be(Polarity.Positive);
    }

    [Fact]
    public void WithScanNumber_SetsScanNumber()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithScanNumber(42)
            .Build();

        // Assert
        scan.ScanNumber.Should().Be(42);
    }

    [Fact]
    public void WithMsOrder_SetsMsOrder()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithMsOrder(2)
            .Build();

        // Assert
        scan.MsOrder.Should().Be(2);
    }

    [Fact]
    public void WithRetentionTime_SetsRetentionTime()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithRetentionTime(15.5)
            .Build();

        // Assert
        scan.RetentionTime.Should().Be(15.5);
    }

    [Fact]
    public void WithSpectrum_SetsSpectrumAndCalculatesAggregates()
    {
        // Arrange
        var mzValues = new[] { 100.0, 200.0, 300.0 };
        var intensityValues = new[] { 1000.0, 5000.0, 2000.0 };

        // Act
        var scan = new MockMsScanBuilder()
            .WithSpectrum(mzValues, intensityValues)
            .Build();

        // Assert
        scan.MzValues.ToArray().Should().BeEquivalentTo(mzValues);
        scan.IntensityValues.ToArray().Should().BeEquivalentTo(intensityValues);
        scan.PeakCount.Should().Be(3);
        scan.BasePeakMz.Should().Be(200.0); // Max intensity at index 1
        scan.BasePeakIntensity.Should().Be(5000.0);
        scan.TotalIonCurrent.Should().Be(8000.0);
    }

    [Fact]
    public void WithBasePeak_OverridesCalculatedBasePeak()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithSpectrum(new[] { 100.0 }, new[] { 1000.0 })
            .WithBasePeak(500.0, 9999.0)
            .Build();

        // Assert
        scan.BasePeakMz.Should().Be(500.0);
        scan.BasePeakIntensity.Should().Be(9999.0);
    }

    [Fact]
    public void WithPrecursor_SetsPrecursorInfoAndMsOrder()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithPrecursor(
                mass: 500.5,
                charge: 2,
                intensity: 10000.0,
                isolationWidth: 2.0,
                collisionEnergy: 35.0,
                fragmentationType: FragmentationType.CID)
            .Build();

        // Assert
        scan.MsOrder.Should().Be(2); // Auto-set to MS2
        scan.PrecursorMass.Should().Be(500.5);
        scan.PrecursorCharge.Should().Be(2);
        scan.PrecursorIntensity.Should().Be(10000.0);
        scan.IsolationWidth.Should().Be(2.0);
        scan.CollisionEnergy.Should().Be(35.0);
        scan.FragmentationType.Should().Be(FragmentationType.CID);
    }

    [Fact]
    public void WithPrecursor_WithMinimalParameters_UsesDefaults()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithPrecursor(mass: 500.0, charge: 2)
            .Build();

        // Assert
        scan.IsolationWidth.Should().Be(1.6);
        scan.CollisionEnergy.Should().Be(30.0);
        scan.FragmentationType.Should().Be(FragmentationType.HCD);
    }

    [Fact]
    public void WithAnalyzer_SetsAnalyzerProperties()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithAnalyzer("Ion Trap", resolution: 30000, massAccuracy: 10.0)
            .Build();

        // Assert
        scan.Analyzer.Should().Be("Ion Trap");
        scan.ResolutionAtMz200.Should().Be(30000);
        scan.MassAccuracyPpm.Should().Be(10.0);
    }

    [Fact]
    public void WithPolarity_SetsPolarity()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithPolarity(Polarity.Negative)
            .Build();

        // Assert
        scan.Polarity.Should().Be(Polarity.Negative);
    }

    [Fact]
    public void WithTrailerExtra_AddsMetadata()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithTrailerExtra("Key1", "Value1")
            .WithTrailerExtra("Key2", "Value2")
            .Build();

        // Assert
        scan.TrailerExtra.Should().HaveCount(2);
        scan.TrailerExtra["Key1"].Should().Be("Value1");
        scan.TrailerExtra["Key2"].Should().Be("Value2");
    }

    [Fact]
    public void UseArrayPool_EnablesArrayPoolUsage()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithSpectrum(new double[100], new double[100])
            .UseArrayPool(true)
            .Build();

        // Assert - verify it doesn't throw on dispose
        scan.Dispose();
    }

    [Fact]
    public void FluentChaining_BuildsCompleteMs2Scan()
    {
        // Act
        var scan = new MockMsScanBuilder()
            .WithScanNumber(100)
            .WithRetentionTime(25.3)
            .WithSpectrum(
                new[] { 150.0, 250.0, 350.0, 450.0 },
                new[] { 500.0, 2000.0, 1500.0, 800.0 })
            .WithPrecursor(600.0, 3, 50000.0)
            .WithAnalyzer("Orbitrap", 60000, 5.0)
            .WithPolarity(Polarity.Positive)
            .WithTrailerExtra("ScanType", "Full ms2")
            .Build();

        // Assert
        scan.ScanNumber.Should().Be(100);
        scan.MsOrder.Should().Be(2);
        scan.RetentionTime.Should().Be(25.3);
        scan.PeakCount.Should().Be(4);
        scan.PrecursorMass.Should().Be(600.0);
        scan.PrecursorCharge.Should().Be(3);
        scan.Analyzer.Should().Be("Orbitrap");
        scan.ResolutionAtMz200.Should().Be(60000);
        scan.TrailerExtra["ScanType"].Should().Be("Full ms2");
    }
}
