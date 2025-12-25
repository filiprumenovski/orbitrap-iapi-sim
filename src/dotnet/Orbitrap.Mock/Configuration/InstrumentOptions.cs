using System.ComponentModel.DataAnnotations;

namespace Orbitrap.Mock.Configuration;

/// <summary>
/// Root configuration for instrument connection.
/// </summary>
public sealed class InstrumentOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Instrument";

    /// <summary>
    /// Which instrument mode to use.
    /// </summary>
    [Required]
    public InstrumentMode Mode { get; set; }

    /// <summary>
    /// Mock instrument configuration. Required when Mode is Mock.
    /// </summary>
    public MockOptions? Mock { get; set; }

    /// <summary>
    /// Real instrument configuration. Required when Mode is Real.
    /// </summary>
    public RealOptions? Real { get; set; }

    /// <summary>
    /// Validates the configuration is complete for the selected mode.
    /// </summary>
    public void Validate()
    {
        switch (Mode)
        {
            case InstrumentMode.Mock:
                if (Mock is null)
                    throw new InvalidOperationException("Mock configuration is required when Mode is Mock");
                Mock.Validate();
                break;

            case InstrumentMode.Real:
                if (Real is null)
                    throw new InvalidOperationException("Real configuration is required when Mode is Real");
                Real.Validate();
                break;

            default:
                throw new InvalidOperationException($"Unknown instrument mode: {Mode}");
        }
    }
}

/// <summary>
/// Instrument mode selection.
/// </summary>
public enum InstrumentMode
{
    /// <summary>Use mock simulator for development and testing.</summary>
    Mock = 0,

    /// <summary>Use real Thermo IAPI for production.</summary>
    Real = 1
}

/// <summary>
/// Configuration for mock instrument connection.
/// </summary>
public sealed class MockOptions
{
    /// <summary>
    /// Host address of the simulator gRPC server.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Port number of the simulator gRPC server.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 31417;

    /// <summary>
    /// Connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to use TLS for gRPC connection.
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Retry policy for connection attempts.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Simulated instrument name.
    /// </summary>
    public string InstrumentName { get; set; } = "Mock Orbitrap Exploris 480";

    /// <summary>
    /// Simulated instrument ID.
    /// </summary>
    public string InstrumentId { get; set; } = "MOCK-001";

    /// <summary>
    /// Full gRPC endpoint address.
    /// </summary>
    public string Endpoint => UseTls
        ? $"https://{Host}:{Port}"
        : $"http://{Host}:{Port}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("Mock Host is required");

        if (Port <= 0 || Port > 65535)
            throw new InvalidOperationException("Mock Port must be between 1 and 65535");
    }
}

/// <summary>
/// Configuration for real Thermo instrument connection.
/// </summary>
public sealed class RealOptions
{
    /// <summary>
    /// COM port for instrument connection.
    /// </summary>
    public string? ComPort { get; set; }

    /// <summary>
    /// Baud rate for serial communication.
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Network address if using ethernet connection.
    /// </summary>
    public string? NetworkAddress { get; set; }

    /// <summary>
    /// Connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Path to Thermo IAPI DLLs if not in standard location.
    /// </summary>
    public string? IapiPath { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ComPort) && string.IsNullOrWhiteSpace(NetworkAddress))
            throw new InvalidOperationException("Either ComPort or NetworkAddress must be specified for Real mode");
    }
}

/// <summary>
/// Retry policy configuration.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    [Range(0, 100)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retries.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries (for exponential backoff).
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Backoff multiplier for exponential retry.
    /// </summary>
    [Range(1.0, 10.0)]
    public double BackoffMultiplier { get; set; } = 2.0;
}
