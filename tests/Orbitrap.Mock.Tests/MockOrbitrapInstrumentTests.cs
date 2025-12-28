using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orbitrap.Abstractions;
using Orbitrap.Mock;
using Orbitrap.Mock.Configuration;

namespace Orbitrap.Mock.Tests;

public class MockOrbitrapInstrumentTests
{
    [Fact]
    public void Constructor_WithOptions_SetsInstrumentProperties()
    {
        // Arrange
        var options = new MockOptions
        {
            InstrumentName = "Test Orbitrap",
            InstrumentId = "TEST-001"
        };

        // Act
        using var instrument = new MockOrbitrapInstrument(Options.Create(options));

        // Assert
        instrument.InstrumentName.Should().Be("Test Orbitrap");
        instrument.InstrumentId.Should().Be("TEST-001");
        instrument.CurrentState.Should().Be(AcquisitionState.Idle);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MockOrbitrapInstrument((MockOptions)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAcquisitionAsync_FromIdle_StartsAcquisition()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var options = new AcquisitionOptions { MaxScans = 5 };

        // Act
        await using var session = await instrument.StartAcquisitionAsync(options);

        // Assert
        instrument.CurrentState.Should().Be(AcquisitionState.Acquiring);
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartAcquisitionAsync_WhenAlreadyAcquiring_ThrowsInvalidOperationException()
    {
        // Arrange
        using var instrument = CreateInstrument();
        await using var session1 = await instrument.StartAcquisitionAsync(new AcquisitionOptions { MaxScans = 100 });

        // Act
        var act = async () => await instrument.StartAcquisitionAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot start acquisition*");
    }

    [Fact]
    public async Task StartAcquisitionAsync_WithMaxScans_StopsAfterMaxScans()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var maxScans = 10;

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = maxScans });

        await session.Completion;

        // Assert
        session.ScanCount.Should().BeLessOrEqualTo(maxScans);
        instrument.CurrentState.Should().Be(AcquisitionState.Completed);
    }

    [Fact]
    public async Task ScanArrived_Event_FiresForEachScan()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var receivedScans = new List<IOrbitrapScan>();

        instrument.ScanArrived += (_, e) => receivedScans.Add(e.Scan);

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 5 });

        await session.Completion;

        // Assert
        receivedScans.Should().HaveCountGreaterThan(0);
        receivedScans.Should().HaveCountLessOrEqualTo(5);
    }

    [Fact]
    public async Task Ms1ScanArrived_Event_FiresOnlyForMs1Scans()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var ms1Scans = new List<IOrbitrapScan>();

        instrument.Ms1ScanArrived += (_, e) => ms1Scans.Add(e.Scan);

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 20 });

        await session.Completion;

        // Assert
        ms1Scans.Should().AllSatisfy(s => s.MsOrder.Should().Be(1));
    }

    [Fact]
    public async Task Ms2ScanArrived_Event_FiresOnlyForMs2Scans()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var ms2Scans = new List<IOrbitrapScan>();

        instrument.Ms2ScanArrived += (_, e) => ms2Scans.Add(e.Scan);

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 20 });

        await session.Completion;

        // Assert
        ms2Scans.Should().AllSatisfy(s => s.MsOrder.Should().BeGreaterThan(1));
    }

    [Fact]
    public async Task Session_Scans_StreamsAllScans()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var scans = new List<IOrbitrapScan>();

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 5 });

        await foreach (var scan in session.Scans)
        {
            scans.Add(scan);
        }

        // Assert
        scans.Should().HaveCountLessOrEqualTo(5);
        scans.Should().AllSatisfy(s =>
        {
            s.ScanNumber.Should().BeGreaterThan(0);
            s.MsOrder.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task Session_StopAsync_StopsAcquisition()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var scans = new List<IOrbitrapScan>();

        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 1000 }); // High limit

        // Act
        instrument.ScanArrived += (_, e) =>
        {
            scans.Add(e.Scan);
            if (scans.Count >= 3)
            {
                _ = session.StopAsync();
            }
        };

        await session.Completion;

        // Assert
        instrument.CurrentState.Should().BeOneOf(
            AcquisitionState.Completed,
            AcquisitionState.Stopping);
    }

    [Fact]
    public async Task Session_ScanCount_TracksScansAcquired()
    {
        // Arrange
        using var instrument = CreateInstrument();

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 10 });

        await session.Completion;

        // Assert
        session.ScanCount.Should().BeLessOrEqualTo(10);
        session.ScanCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AutoFreeze_WhenEnabled_ReturnsFrozenScans()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var scans = new List<IOrbitrapScan>();

        instrument.ScanArrived += (_, e) => scans.Add(e.Scan);

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 5, AutoFreeze = true });

        await session.Completion;

        // Assert
        scans.Should().AllSatisfy(s => s.Should().BeOfType<FrozenOrbitrapScan>());
    }

    [Fact]
    public async Task GetScansAsync_WithFilter_AppliesFilter()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var filter = new ScanFilter { MsOrder = 1 };
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Start acquisition in background
        _ = instrument.StartAcquisitionAsync(new AcquisitionOptions { MaxScans = 50 }, cts.Token);

        // Small delay to let acquisition start
        await Task.Delay(100);

        // Act
        var ms1Scans = new List<IOrbitrapScan>();
        await foreach (var scan in instrument.GetScansAsync(filter, cts.Token))
        {
            ms1Scans.Add(scan);
            if (ms1Scans.Count >= 3) break;
        }

        // Assert
        ms1Scans.Should().AllSatisfy(s => s.MsOrder.Should().Be(1));
    }

    [Fact]
    public async Task Dispose_WhileAcquiring_StopsAcquisition()
    {
        // Arrange
        var instrument = CreateInstrument();
        var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 1000 });

        // Wait a bit for acquisition to actually be running
        await Task.Delay(50);

        // Act
        await instrument.DisposeAsync();

        // Assert - should not hang or throw, state should be completed or idle
        instrument.CurrentState.Should().BeOneOf(AcquisitionState.Completed, AcquisitionState.Idle);
    }

    [Fact]
    public void Dispose_WhenIdle_CompletesSuccessfully()
    {
        // Arrange
        var instrument = CreateInstrument();

        // Act & Assert - should not throw
        instrument.Dispose();
    }

    [Fact]
    public async Task GeneratedScans_HaveRealisticData()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var scans = new List<IOrbitrapScan>();

        instrument.ScanArrived += (_, e) => scans.Add(e.Scan);

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 10 });

        await session.Completion;

        // Assert - verify scans have realistic data
        foreach (var scan in scans)
        {
            scan.PeakCount.Should().BeGreaterThan(0);
            scan.TotalIonCurrent.Should().BeGreaterThan(0);
            scan.BasePeakIntensity.Should().BeGreaterThan(0);
            scan.Analyzer.Should().Be("Orbitrap");
            scan.RetentionTime.Should().BeGreaterThanOrEqualTo(0);

            // MS2 scans should have precursor info
            if (scan.MsOrder == 2)
            {
                scan.PrecursorMass.Should().NotBeNull();
                scan.PrecursorCharge.Should().NotBeNull();
            }
        }
    }

    [Fact]
    public async Task Session_SessionId_IsUnique()
    {
        // Arrange
        using var instrument = CreateInstrument();
        var sessionIds = new HashSet<string>();

        // Act
        for (int i = 0; i < 3; i++)
        {
            await using var session = await instrument.StartAcquisitionAsync(
                new AcquisitionOptions { MaxScans = 1 });
            sessionIds.Add(session.SessionId);
            await session.Completion;
        }

        // Assert
        sessionIds.Should().HaveCount(3); // All unique
    }

    private static MockOrbitrapInstrument CreateInstrument()
    {
        return new MockOrbitrapInstrument(Options.Create(new MockOptions
        {
            InstrumentName = "Test Instrument",
            InstrumentId = "TEST-001"
        }));
    }
}
