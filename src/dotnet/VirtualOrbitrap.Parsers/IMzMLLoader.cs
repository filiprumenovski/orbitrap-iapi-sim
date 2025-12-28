using VirtualOrbitrap.Parsers.Dto;

namespace VirtualOrbitrap.Parsers;

/// <summary>
/// Interface for mzML file loaders.
/// </summary>
public interface IMzMLLoader
{
    /// <summary>
    /// Load and parse an mzML file.
    /// </summary>
    /// <param name="filePath">Path to the mzML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed raw file with all scans.</returns>
    Task<ParsedRawFile> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream scans from an mzML file without loading entire file into memory.
    /// </summary>
    /// <param name="filePath">Path to the mzML file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of parsed scans.</returns>
    IAsyncEnumerable<ParsedScan> StreamScansAsync(string filePath, CancellationToken cancellationToken = default);
}
