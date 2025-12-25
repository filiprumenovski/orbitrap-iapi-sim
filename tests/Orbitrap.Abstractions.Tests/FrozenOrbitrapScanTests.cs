using Xunit;
using FluentAssertions;
using Orbitrap.Abstractions;

namespace Orbitrap.Abstractions.Tests;

public class FrozenOrbitrapScanTests
{
    [Fact]
    public void Constructor_WithExplicitValues_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var mzValues = new[] { 100.0, 200.0, 300.0 };
        var intensityValues = new[] { 1000.0, 5000.0, 2000.0 };
        var trailerExtra = new Dictionary<string, string> { { "Key1", "Value1" } };

        // Act
        var scan = new FrozenOrbitrapScan(
            scanNumber: 42,
            msOrder: 2,
            retentionTime: 15.5,
            mzValues: mzValues,
            intensityValues: intensityValues,
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
            polarity: Polarity.Positive,
            trailerExtra: trailerExtra);

        // Assert
        scan.ScanNumber.Should().Be(42);
        scan.MsOrder.Should().Be(2);
        scan.RetentionTime.Should().Be(15.5);
        scan.MzValues.ToArray().Should().BeEquivalentTo(mzValues);
        scan.IntensityValues.ToArray().Should().BeEquivalentTo(intensityValues);
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
        scan.TrailerExtra.Should().ContainKey("Key1").WhoseValue.Should().Be("Value1");
    }

    [Fact]
    public void Constructor_WithNullArrays_CreatesEmptyArrays()
    {
        // Act
        var scan = new FrozenOrbitrapScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 0.0,
            mzValues: null!,
            intensityValues: null!,
            basePeakMz: 0,
            basePeakIntensity: 0,
            totalIonCurrent: 0,
            precursorMass: null,
            precursorCharge: null,
            precursorIntensity: null,
            isolationWidth: null,
            collisionEnergy: null,
            fragmentationType: null,
            analyzer: null!,
            resolutionAtMz200: 0,
            massAccuracyPpm: 0,
            polarity: Polarity.Unknown);

        // Assert
        scan.MzValues.Length.Should().Be(0);
        scan.IntensityValues.Length.Should().Be(0);
        scan.PeakCount.Should().Be(0);
        scan.Analyzer.Should().Be("Unknown");
    }

    [Fact]
    public void Constructor_FromIOrbitrapScan_CopiesAllValues()
    {
        // Arrange
        var source = CreateTestScan();

        // Act
        var frozen = new FrozenOrbitrapScan(source);

        // Assert
        frozen.ScanNumber.Should().Be(source.ScanNumber);
        frozen.MsOrder.Should().Be(source.MsOrder);
        frozen.RetentionTime.Should().Be(source.RetentionTime);
        frozen.MzValues.ToArray().Should().BeEquivalentTo(source.MzValues.ToArray());
        frozen.IntensityValues.ToArray().Should().BeEquivalentTo(source.IntensityValues.ToArray());
        frozen.BasePeakMz.Should().Be(source.BasePeakMz);
        frozen.BasePeakIntensity.Should().Be(source.BasePeakIntensity);
        frozen.TotalIonCurrent.Should().Be(source.TotalIonCurrent);
        frozen.PrecursorMass.Should().Be(source.PrecursorMass);
        frozen.PrecursorCharge.Should().Be(source.PrecursorCharge);
        frozen.Analyzer.Should().Be(source.Analyzer);
        frozen.Polarity.Should().Be(source.Polarity);
    }

    [Fact]
    public void Constructor_FromIOrbitrapScan_CreatesDefensiveCopy()
    {
        // Arrange
        var originalMz = new[] { 100.0, 200.0, 300.0 };
        var originalIntensity = new[] { 1000.0, 2000.0, 3000.0 };
        var source = new TestOrbitrapScan(originalMz, originalIntensity);

        // Act
        var frozen = new FrozenOrbitrapScan(source);

        // Modify original arrays
        originalMz[0] = 999.0;
        originalIntensity[0] = 999.0;

        // Assert - frozen should not be affected
        frozen.MzValues.Span[0].Should().Be(100.0);
        frozen.IntensityValues.Span[0].Should().Be(1000.0);
    }

    [Fact]
    public void ToFrozen_ReturnsSelf()
    {
        // Arrange
        var scan = CreateFrozenTestScan();

        // Act
        var result = scan.ToFrozen();

        // Assert
        result.Should().BeSameAs(scan);
    }

    [Fact]
    public void TrailerExtra_IsFrozenDictionary_AndImmutable()
    {
        // Arrange
        var trailerExtra = new Dictionary<string, string>
        {
            { "Key1", "Value1" },
            { "Key2", "Value2" }
        };

        var scan = new FrozenOrbitrapScan(
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
            polarity: Polarity.Positive,
            trailerExtra: trailerExtra);

        // Modify original dictionary
        trailerExtra["Key1"] = "Modified";
        trailerExtra["Key3"] = "NewValue";

        // Assert - frozen should not be affected
        scan.TrailerExtra["Key1"].Should().Be("Value1");
        scan.TrailerExtra.Should().NotContainKey("Key3");
        scan.TrailerExtra.Count.Should().Be(2);
    }

    [Fact]
    public void ReadOnlyMemory_SupportsSlicing()
    {
        // Arrange
        var mzValues = new[] { 100.0, 200.0, 300.0, 400.0, 500.0 };
        var scan = new FrozenOrbitrapScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 0,
            mzValues: mzValues,
            intensityValues: new double[5],
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

        // Act - slice without copying
        var slice = scan.MzValues.Slice(1, 3);

        // Assert
        slice.Length.Should().Be(3);
        slice.Span[0].Should().Be(200.0);
        slice.Span[1].Should().Be(300.0);
        slice.Span[2].Should().Be(400.0);
    }

    private static FrozenOrbitrapScan CreateFrozenTestScan()
    {
        return new FrozenOrbitrapScan(
            scanNumber: 1,
            msOrder: 1,
            retentionTime: 1.5,
            mzValues: new[] { 100.0, 200.0 },
            intensityValues: new[] { 1000.0, 2000.0 },
            basePeakMz: 200.0,
            basePeakIntensity: 2000.0,
            totalIonCurrent: 3000.0,
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

    private static TestOrbitrapScan CreateTestScan()
    {
        return new TestOrbitrapScan(
            new[] { 100.0, 200.0 },
            new[] { 1000.0, 2000.0 });
    }

    /// <summary>
    /// Simple IOrbitrapScan implementation for testing
    /// </summary>
    private class TestOrbitrapScan : IOrbitrapScan
    {
        private readonly double[] _mzValues;
        private readonly double[] _intensityValues;

        public TestOrbitrapScan(double[] mzValues, double[] intensityValues)
        {
            _mzValues = mzValues;
            _intensityValues = intensityValues;
        }

        public int ScanNumber => 1;
        public int MsOrder => 1;
        public double RetentionTime => 1.5;
        public ReadOnlyMemory<double> MzValues => _mzValues;
        public ReadOnlyMemory<double> IntensityValues => _intensityValues;
        public int PeakCount => _mzValues.Length;
        public double BasePeakMz => 200.0;
        public double BasePeakIntensity => 2000.0;
        public double TotalIonCurrent => 3000.0;
        public double? PrecursorMass => null;
        public int? PrecursorCharge => null;
        public double? PrecursorIntensity => null;
        public double? IsolationWidth => null;
        public double? CollisionEnergy => null;
        public FragmentationType? FragmentationType => null;
        public string Analyzer => "Orbitrap";
        public double ResolutionAtMz200 => 120000;
        public double MassAccuracyPpm => 3.0;
        public Polarity Polarity => Polarity.Positive;
        public IReadOnlyDictionary<string, string> TrailerExtra => new Dictionary<string, string>();

        public FrozenOrbitrapScan ToFrozen() => new(this);
    }
}
