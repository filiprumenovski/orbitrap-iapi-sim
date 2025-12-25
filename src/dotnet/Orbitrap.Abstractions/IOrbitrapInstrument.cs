using System.Runtime.CompilerServices;

namespace Orbitrap.Abstractions;

/// <summary>
/// Unified instrument interface: real Orbitrap and mock both implement this.
/// Downstream code depends ONLY on this, enabling seamless switching between real and mock.
/// </summary>
public interface IOrbitrapInstrument : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Human-readable instrument name (e.g., "Orbitrap Exploris 480").
    /// </summary>
    string InstrumentName { get; }

    /// <summary>
    /// Unique instrument identifier (serial number or mock instance ID).
    /// </summary>
    string InstrumentId { get; }

    /// <summary>
    /// Current acquisition state.
    /// </summary>
    AcquisitionState CurrentState { get; }

    /// <summary>
    /// Event-based API: fires when any scan arrives.
    /// Use for simple scenarios or backwards compatibility.
    /// </summary>
    event EventHandler<OrbitrapScanEventArgs>? ScanArrived;

    /// <summary>
    /// Event-based API: fires specifically for MS1 scans.
    /// </summary>
    event EventHandler<OrbitrapScanEventArgs>? Ms1ScanArrived;

    /// <summary>
    /// Event-based API: fires specifically for MS2+ scans.
    /// </summary>
    event EventHandler<OrbitrapScanEventArgs>? Ms2ScanArrived;

    /// <summary>
    /// Starts an acquisition session with optional configuration.
    /// Returns a session object for lifecycle management and scan streaming.
    /// </summary>
    Task<IAcquisitionSession> StartAcquisitionAsync(
        AcquisitionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Modern async stream API: yields scans as they arrive.
    /// Provides natural backpressure handling and works with LINQ.
    /// </summary>
    IAsyncEnumerable<IOrbitrapScan> GetScansAsync(
        ScanFilter? filter = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an active acquisition session with lifecycle management.
/// </summary>
public interface IAcquisitionSession : IAsyncDisposable
{
    /// <summary>
    /// Unique identifier for this acquisition session.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Current state of the acquisition.
    /// </summary>
    AcquisitionState State { get; }

    /// <summary>
    /// Number of scans acquired in this session.
    /// </summary>
    long ScanCount { get; }

    /// <summary>
    /// Task that completes when the acquisition ends (success, error, or cancellation).
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Exception that caused the acquisition to fail, if any.
    /// </summary>
    Exception? Error { get; }

    /// <summary>
    /// Async stream of scans for this session.
    /// </summary>
    IAsyncEnumerable<IOrbitrapScan> Scans { get; }

    /// <summary>
    /// Request graceful stop of the acquisition.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause the acquisition (if supported).
    /// </summary>
    Task PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume a paused acquisition.
    /// </summary>
    Task ResumeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Acquisition state machine.
/// </summary>
public enum AcquisitionState
{
    /// <summary>No acquisition in progress.</summary>
    Idle = 0,

    /// <summary>Acquisition starting up.</summary>
    Starting,

    /// <summary>Actively acquiring scans.</summary>
    Acquiring,

    /// <summary>Acquisition paused.</summary>
    Paused,

    /// <summary>Acquisition stopping.</summary>
    Stopping,

    /// <summary>Acquisition completed successfully.</summary>
    Completed,

    /// <summary>Acquisition failed with an error.</summary>
    Faulted
}

/// <summary>
/// Options for configuring an acquisition session.
/// </summary>
public sealed record AcquisitionOptions
{
    /// <summary>
    /// Maximum number of scans to acquire. Null for unlimited.
    /// </summary>
    public int? MaxScans { get; init; }

    /// <summary>
    /// Maximum acquisition duration. Null for unlimited.
    /// </summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>
    /// Filter to apply to incoming scans.
    /// </summary>
    public ScanFilter? Filter { get; init; }

    /// <summary>
    /// Buffer capacity for scan channel. Higher values handle bursty data better.
    /// </summary>
    public int BufferCapacity { get; init; } = 1000;

    /// <summary>
    /// Whether to automatically create frozen copies of scans for thread safety.
    /// </summary>
    public bool AutoFreeze { get; init; } = false;
}

/// <summary>
/// Filter criteria for scan selection.
/// </summary>
public sealed record ScanFilter
{
    /// <summary>
    /// Filter by MS order (1 = MS1, 2 = MS2, etc.). Null for all.
    /// </summary>
    public int? MsOrder { get; init; }

    /// <summary>
    /// Minimum m/z value. Null for no minimum.
    /// </summary>
    public double? MinMz { get; init; }

    /// <summary>
    /// Maximum m/z value. Null for no maximum.
    /// </summary>
    public double? MaxMz { get; init; }

    /// <summary>
    /// Minimum retention time in minutes. Null for no minimum.
    /// </summary>
    public double? MinRetentionTime { get; init; }

    /// <summary>
    /// Maximum retention time in minutes. Null for no maximum.
    /// </summary>
    public double? MaxRetentionTime { get; init; }

    /// <summary>
    /// Filter by polarity. Null for all.
    /// </summary>
    public Polarity? Polarity { get; init; }

    /// <summary>
    /// Filter by analyzer type. Null for all.
    /// </summary>
    public string? Analyzer { get; init; }

    /// <summary>
    /// Checks if a scan passes this filter.
    /// </summary>
    public bool Matches(IOrbitrapScan scan)
    {
        if (MsOrder.HasValue && scan.MsOrder != MsOrder.Value)
            return false;

        if (MinRetentionTime.HasValue && scan.RetentionTime < MinRetentionTime.Value)
            return false;

        if (MaxRetentionTime.HasValue && scan.RetentionTime > MaxRetentionTime.Value)
            return false;

        if (Polarity.HasValue && scan.Polarity != Polarity.Value)
            return false;

        if (Analyzer is not null && !string.Equals(scan.Analyzer, Analyzer, StringComparison.OrdinalIgnoreCase))
            return false;

        // m/z range filtering would require checking spectrum peaks
        // Typically done downstream, but basic check here if needed

        return true;
    }
}
