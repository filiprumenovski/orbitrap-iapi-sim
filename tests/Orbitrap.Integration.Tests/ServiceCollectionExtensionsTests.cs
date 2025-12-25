using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orbitrap.Abstractions;
using Orbitrap.Integration;
using Orbitrap.Mock;
using Orbitrap.Mock.Configuration;

namespace Orbitrap.Integration.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOrbitrapInstrument_WithAction_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddOrbitrapInstrument(options =>
        {
            options.Mode = InstrumentMode.Mock;
            options.Mock = new MockOptions
            {
                InstrumentName = "DI Test Instrument"
            };
        });

        var provider = services.BuildServiceProvider();
        var instrument = provider.GetRequiredService<IOrbitrapInstrument>();

        // Assert
        instrument.Should().NotBeNull();
        instrument.InstrumentName.Should().Be("DI Test Instrument");
    }

    [Fact]
    public void AddOrbitrapInstrument_WithExplicitOptions_UsesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Mock,
            Mock = new MockOptions
            {
                InstrumentId = "EXPLICIT-001"
            }
        };

        // Act
        services.AddOrbitrapInstrument(options);

        var provider = services.BuildServiceProvider();
        var instrument = provider.GetRequiredService<IOrbitrapInstrument>();

        // Assert
        instrument.InstrumentId.Should().Be("EXPLICIT-001");
    }

    [Fact]
    public void AddOrbitrapInstrument_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOrbitrapInstrument(options =>
        {
            options.Mode = InstrumentMode.Mock;
            options.Mock = new MockOptions();
        });

        var provider = services.BuildServiceProvider();

        // Act
        var instrument1 = provider.GetRequiredService<IOrbitrapInstrument>();
        var instrument2 = provider.GetRequiredService<IOrbitrapInstrument>();

        // Assert
        instrument1.Should().BeSameAs(instrument2);
    }

    [Fact]
    public void AddMockOrbitrapInstrument_WithConfigure_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMockOrbitrapInstrument(mock =>
        {
            mock.InstrumentName = "Configured Mock";
            mock.Port = 12345;
        });

        var provider = services.BuildServiceProvider();
        var instrument = provider.GetRequiredService<IOrbitrapInstrument>();

        // Assert
        instrument.Should().BeOfType<MockOrbitrapInstrument>();
        instrument.InstrumentName.Should().Be("Configured Mock");
    }

    [Fact]
    public void AddMockOrbitrapInstrument_WithoutConfigure_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMockOrbitrapInstrument();

        var provider = services.BuildServiceProvider();
        var instrument = provider.GetRequiredService<IOrbitrapInstrument>();

        // Assert
        instrument.InstrumentName.Should().Be("Mock Orbitrap Exploris 480");
    }

    [Fact]
    public void AddMockOrbitrapInstrumentDefault_RegistersDefaultMock()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMockOrbitrapInstrumentDefault();

        var provider = services.BuildServiceProvider();
        var instrument = provider.GetRequiredService<IOrbitrapInstrument>();

        // Assert
        instrument.Should().NotBeNull();
        instrument.Should().BeOfType<MockOrbitrapInstrument>();
    }

    [Fact]
    public void AddOrbitrapInstrument_DoesNotOverwriteExistingRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // First registration
        services.AddMockOrbitrapInstrument(mock => mock.InstrumentId = "FIRST");

        // Second registration (should be ignored due to TryAdd)
        services.AddMockOrbitrapInstrument(mock => mock.InstrumentId = "SECOND");

        var provider = services.BuildServiceProvider();

        // Act
        var instrument = provider.GetRequiredService<IOrbitrapInstrument>();

        // Assert
        instrument.InstrumentId.Should().Be("FIRST");
    }

    [Fact]
    public async Task RegisteredInstrument_CanBeUsedForAcquisition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMockOrbitrapInstrumentDefault();

        var provider = services.BuildServiceProvider();
        var instrument = provider.GetRequiredService<IOrbitrapInstrument>();

        // Act
        await using var session = await instrument.StartAcquisitionAsync(
            new AcquisitionOptions { MaxScans = 3 });

        var scans = new List<IOrbitrapScan>();
        await foreach (var scan in session.Scans)
        {
            scans.Add(scan);
        }

        // Assert
        scans.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void AddOrbitrapInstrument_WithNullServices_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ServiceCollectionExtensions.AddOrbitrapInstrument(null!, _ => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddOrbitrapInstrument_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddOrbitrapInstrument((Action<InstrumentOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddOrbitrapInstrument_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddOrbitrapInstrument((InstrumentOptions)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
