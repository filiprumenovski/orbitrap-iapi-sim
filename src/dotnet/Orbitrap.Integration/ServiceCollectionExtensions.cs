using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orbitrap.Abstractions;
using Orbitrap.Mock.Configuration;

namespace Orbitrap.Integration;

/// <summary>
/// Extension methods for configuring Orbitrap services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Orbitrap instrument services to the service collection.
    /// Configuration is read from the "Instrument" section.
    /// </summary>
    public static IServiceCollection AddOrbitrapInstrument(
        this IServiceCollection services,
        Action<InstrumentOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddSingleton<IOrbitrapInstrument>(sp =>
            InstrumentFactory.Create(sp.GetRequiredService<IOptions<InstrumentOptions>>()));

        return services;
    }

    /// <summary>
    /// Adds Orbitrap instrument services using explicit options.
    /// </summary>
    public static IServiceCollection AddOrbitrapInstrument(
        this IServiceCollection services,
        InstrumentOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(Options.Create(options));
        services.TryAddSingleton<IOrbitrapInstrument>(sp =>
            InstrumentFactory.Create(sp.GetRequiredService<IOptions<InstrumentOptions>>()));

        return services;
    }

    /// <summary>
    /// Adds a mock Orbitrap instrument with the specified options.
    /// </summary>
    public static IServiceCollection AddMockOrbitrapInstrument(
        this IServiceCollection services,
        Action<MockOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var mockOptions = new MockOptions();
        configure?.Invoke(mockOptions);

        var instrumentOptions = new InstrumentOptions
        {
            Mode = InstrumentMode.Mock,
            Mock = mockOptions
        };

        return services.AddOrbitrapInstrument(instrumentOptions);
    }

    /// <summary>
    /// Adds a mock Orbitrap instrument with default settings.
    /// Useful for quick testing scenarios.
    /// </summary>
    public static IServiceCollection AddMockOrbitrapInstrumentDefault(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IOrbitrapInstrument>(_ => InstrumentFactory.CreateMockDefault());

        return services;
    }
}
