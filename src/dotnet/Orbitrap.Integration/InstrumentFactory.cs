using Microsoft.Extensions.Options;
using Orbitrap.Abstractions;
using Orbitrap.Mock;
using Orbitrap.Mock.Configuration;

namespace Orbitrap.Integration;

/// <summary>
/// Factory for creating IOrbitrapInstrument instances.
/// Abstracts away the decision between mock and real implementations.
/// </summary>
public static class InstrumentFactory
{
    /// <summary>
    /// Creates an IOrbitrapInstrument based on configuration.
    /// </summary>
    public static IOrbitrapInstrument Create(IOptions<InstrumentOptions> options)
    {
        return Create(options.Value);
    }

    /// <summary>
    /// Creates an IOrbitrapInstrument based on configuration.
    /// </summary>
    public static IOrbitrapInstrument Create(InstrumentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return options.Mode switch
        {
            InstrumentMode.Mock => CreateMock(options.Mock!),
            InstrumentMode.Real => CreateReal(options.Real!),
            _ => throw new ArgumentOutOfRangeException(nameof(options), $"Unknown mode: {options.Mode}")
        };
    }

    /// <summary>
    /// Creates a mock instrument with the specified options.
    /// </summary>
    public static IOrbitrapInstrument CreateMock(MockOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new MockOrbitrapInstrument(Options.Create(options));
    }

    /// <summary>
    /// Creates a mock instrument with default options.
    /// Useful for quick testing scenarios.
    /// </summary>
    public static IOrbitrapInstrument CreateMockDefault()
    {
        return new MockOrbitrapInstrument(Options.Create(new MockOptions()));
    }

    /// <summary>
    /// Creates a real Thermo IAPI instrument.
    /// Only available on Windows with Thermo DLLs installed.
    /// </summary>
    private static IOrbitrapInstrument CreateReal(RealOptions options)
    {
        // This would be implemented when Thermo DLLs are available
        // For now, throw a clear error

#if WINDOWS
        // TODO: Implement when Thermo.TNG.Client is available
        // return new RealOrbitrapAdapter(options);
        throw new NotImplementedException(
            "Real Thermo IAPI adapter is not yet implemented. " +
            "Requires Thermo.TNG.Client DLL and Windows platform.");
#else
        throw new PlatformNotSupportedException(
            "Real Thermo IAPI is only supported on Windows. " +
            "Use Mock mode for cross-platform development.");
#endif
    }
}
