using Xunit;
using FluentAssertions;
using Orbitrap.Mock.Configuration;

namespace Orbitrap.Mock.Tests;

public class InstrumentOptionsTests
{
    [Fact]
    public void Validate_MockMode_WithValidMockOptions_DoesNotThrow()
    {
        // Arrange
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Mock,
            Mock = new MockOptions
            {
                Host = "localhost",
                Port = 31417
            }
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MockMode_WithoutMockOptions_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Mock,
            Mock = null
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Mock configuration is required*");
    }

    [Fact]
    public void Validate_RealMode_WithValidRealOptions_DoesNotThrow()
    {
        // Arrange
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Real,
            Real = new RealOptions
            {
                ComPort = "COM1"
            }
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RealMode_WithoutRealOptions_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new InstrumentOptions
        {
            Mode = InstrumentMode.Real,
            Real = null
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Real configuration is required*");
    }
}

public class MockOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        // Act
        var options = new MockOptions();

        // Assert
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(31417);
        options.UseTls.Should().BeFalse();
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.InstrumentName.Should().Be("Mock Orbitrap Exploris 480");
        options.InstrumentId.Should().Be("MOCK-001");
    }

    [Fact]
    public void Endpoint_WithoutTls_ReturnsHttpEndpoint()
    {
        // Arrange
        var options = new MockOptions
        {
            Host = "localhost",
            Port = 12345,
            UseTls = false
        };

        // Act & Assert
        options.Endpoint.Should().Be("http://localhost:12345");
    }

    [Fact]
    public void Endpoint_WithTls_ReturnsHttpsEndpoint()
    {
        // Arrange
        var options = new MockOptions
        {
            Host = "server.example.com",
            Port = 443,
            UseTls = true
        };

        // Act & Assert
        options.Endpoint.Should().Be("https://server.example.com:443");
    }

    [Fact]
    public void Validate_WithEmptyHost_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new MockOptions
        {
            Host = "",
            Port = 31417
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Host is required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Validate_WithInvalidPort_ThrowsInvalidOperationException(int port)
    {
        // Arrange
        var options = new MockOptions
        {
            Host = "localhost",
            Port = port
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Port must be between*");
    }
}

public class RealOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        // Act
        var options = new RealOptions();

        // Assert
        options.ComPort.Should().BeNull();
        options.BaudRate.Should().Be(9600);
        options.NetworkAddress.Should().BeNull();
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(60));
        options.IapiPath.Should().BeNull();
    }

    [Fact]
    public void Validate_WithComPort_DoesNotThrow()
    {
        // Arrange
        var options = new RealOptions { ComPort = "COM1" };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithNetworkAddress_DoesNotThrow()
    {
        // Arrange
        var options = new RealOptions { NetworkAddress = "192.168.1.100" };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithNeitherComPortNorNetwork_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new RealOptions();

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ComPort or NetworkAddress*");
    }
}

public class RetryOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        // Act
        var options = new RetryOptions();

        // Assert
        options.MaxRetries.Should().Be(3);
        options.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxDelay.Should().Be(TimeSpan.FromSeconds(30));
        options.BackoffMultiplier.Should().Be(2.0);
    }
}
