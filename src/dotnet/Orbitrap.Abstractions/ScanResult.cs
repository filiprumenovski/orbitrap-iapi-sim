namespace Orbitrap.Abstractions;

/// <summary>
/// Result type for scan operations that can fail.
/// Provides explicit error handling without exceptions.
/// </summary>
public readonly struct ScanResult
{
    private ScanResult(bool isSuccess, IOrbitrapScan? scan, ScanError? error)
    {
        IsSuccess = isSuccess;
        Scan = scan;
        Error = error;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The scan if successful, null otherwise.
    /// </summary>
    public IOrbitrapScan? Scan { get; }

    /// <summary>
    /// The error if failed, null otherwise.
    /// </summary>
    public ScanError? Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ScanResult Success(IOrbitrapScan scan) =>
        new(true, scan ?? throw new ArgumentNullException(nameof(scan)), null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ScanResult Failure(ScanError error) =>
        new(false, null, error ?? throw new ArgumentNullException(nameof(error)));

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static ScanResult Failure(string code, string message, Exception? inner = null) =>
        new(false, null, new ScanError(code, message, inner));

    /// <summary>
    /// Executes the appropriate action based on success or failure.
    /// </summary>
    public T Match<T>(Func<IOrbitrapScan, T> onSuccess, Func<ScanError, T> onFailure)
    {
        return IsSuccess ? onSuccess(Scan!) : onFailure(Error!);
    }

    /// <summary>
    /// Executes the appropriate action based on success or failure.
    /// </summary>
    public void Match(Action<IOrbitrapScan> onSuccess, Action<ScanError> onFailure)
    {
        if (IsSuccess)
            onSuccess(Scan!);
        else
            onFailure(Error!);
    }

    /// <summary>
    /// Returns the scan or throws if failed.
    /// </summary>
    public IOrbitrapScan GetScanOrThrow()
    {
        if (IsSuccess)
            return Scan!;

        throw new ScanException(Error!);
    }
}

/// <summary>
/// Error information for scan operations.
/// </summary>
public sealed record ScanError(
    string Code,
    string Message,
    Exception? InnerException = null)
{
    /// <summary>Error reading scan data from instrument.</summary>
    public static readonly string ReadError = "SCAN_READ_ERROR";

    /// <summary>Scan data is corrupted or invalid.</summary>
    public static readonly string InvalidData = "SCAN_INVALID_DATA";

    /// <summary>Communication timeout with instrument.</summary>
    public static readonly string Timeout = "SCAN_TIMEOUT";

    /// <summary>Buffer overflow - consumer too slow.</summary>
    public static readonly string BufferOverflow = "SCAN_BUFFER_OVERFLOW";

    /// <summary>Acquisition was cancelled.</summary>
    public static readonly string Cancelled = "SCAN_CANCELLED";

    /// <summary>Instrument connection lost.</summary>
    public static readonly string Disconnected = "SCAN_DISCONNECTED";
}

/// <summary>
/// Exception thrown when a scan operation fails.
/// </summary>
public sealed class ScanException : Exception
{
    public ScanException(ScanError error)
        : base(error.Message, error.InnerException)
    {
        Error = error;
    }

    /// <summary>
    /// The underlying error.
    /// </summary>
    public ScanError Error { get; }
}
