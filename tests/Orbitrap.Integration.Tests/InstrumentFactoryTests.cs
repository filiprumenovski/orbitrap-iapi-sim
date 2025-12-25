using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orbitrap.Abstractions;
using Orbitrap.Integration;
using Orbitrap.Mock;
using Orbitrap.Mock.Configuration;

namespace Orbitrap.Integration.Tests;

public class InstrumentFactoryTests
{
    [Fact]
    public void Create_WithMockMode_CreatesMockInstrument()
    {
        // Arrange
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Mock,
            Mock = new MockOptions
            {
                InstrumentName = "Test Mock",
                InstrumentId = "MOCK-TEST-001"
            }
        };

        // Act
        using var instrument = InstrumentFactory.Create(options);

        // Assert
        instrument.Should().NotBeNull();
        instrument.Should().BeOfType<MockOrbitrapInstrument>();
        instrument.InstrumentName.Should().Be("Test Mock");
        instrument.InstrumentId.Should().Be("MOCK-TEST-001");
    }

    [Fact]
    public void Create_WithIOptions_CreatesMockInstrument()
    {
        // Arrange
        var options = Options.Create(new InstrumentOptions
        {
            Mode = InstrumentMode.Mock,
            Mock = new MockOptions()
        });

        // Act
        using var instrument = InstrumentFactory.Create(options);

        // Assert
        instrument.Should().NotBeNull();
        instrument.Should().BeOfType<MockOrbitrapInstrument>();
    }

    [Fact]
    public void Create_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => InstrumentFactory.Create((InstrumentOptions)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithInvalidOptions_ThrowsInvalidOperationException()
    {
        // Arrange - Mock mode without Mock options
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Mock,
            Mock = null
        };

        // Act
        var act = () => InstrumentFactory.Create(options);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateMock_WithOptions_CreatesMockInstrument()
    {
        // Arrange
        var options = new MockOptions
        {
            InstrumentName = "Custom Mock",
            InstrumentId = "CUSTOM-001"
        };

        // Act
        using var instrument = InstrumentFactory.CreateMock(options);

        // Assert
        instrument.Should().NotBeNull();
        instrument.InstrumentName.Should().Be("Custom Mock");
        instrument.InstrumentId.Should().Be("CUSTOM-001");
    }

    [Fact]
    public void CreateMock_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => InstrumentFactory.CreateMock(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateMockDefault_CreatesInstrumentWithDefaults()
    {
        // Act
        using var instrument = InstrumentFactory.CreateMockDefault();

        // Assert
        instrument.Should().NotBeNull();
        instrument.Should().BeOfType<MockOrbitrapInstrument>();
        instrument.InstrumentName.Should().Be("Mock Orbitrap Exploris 480");
        instrument.InstrumentId.Should().Be("MOCK-001");
    }

    [Fact]
    public void Create_WithRealMode_OnNonWindows_ThrowsPlatformNotSupportedException()
    {
        // This test will behave differently on Windows vs other platforms
        // Arrange
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Real,
            Real = new RealOptions { ComPort = "COM1" }
        };

        // Act
        var act = () => InstrumentFactory.Create(options);

        // Assert - on non-Windows, should throw PlatformNotSupportedException
        // on Windows, should throw NotImplementedException (until real adapter is implemented)
        act.Should().Throw<Exception>();
    }

    [Fact]
    public async Task CreatedMockInstrument_CanAcquireScans()
    {
        // Arrange
        using var instrument = InstrumentFactory.CreateMockDefault();
        var scans = new List<IOrbitrapScan>();

        instrument.ScanArrived += (_, e) => scans.Add(e.Scan);

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 5 });

        await session.Completion;

        // Assert
        scans.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Create_MultipleInstruments_AreIndependent()
    {
        // Arrange & Act
        using var instrument1 = InstrumentFactory.CreateMockDefault();
        using var instrument2 = InstrumentFactory.CreateMockDefault();

        // Assert
        instrument1.Should().NotBeSameAs(instrument2);
        instrument1.CurrentState.Should().Be(AcquisitionState.Idle);
        instrument2.CurrentState.Should().Be(AcquisitionState.Idle);
    }
}
