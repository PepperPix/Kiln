namespace Kiln.Services;

/// <summary>
/// Locates the Pagefind executable used to build the search index.
/// </summary>
public interface IPagefindBinaryProvider
{
    /// <summary>
    /// Resolves the path of the Pagefind executable.
    /// </summary>
    /// <param name="extended">Whether the extended edition of Pagefind is required.</param>
    /// <param name="allowDownload">Whether the executable may be downloaded when it cannot be found locally.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The path of the executable.</returns>
    /// <exception cref="InvalidOperationException">The executable cannot be found and downloading is not allowed or fails.</exception>
    /// <exception cref="PlatformNotSupportedException">No Pagefind build exists for the current platform.</exception>
    Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, CancellationToken ct);

    /// <summary>Resolves the binary, preferring <paramref name="configuredPath"/> (search.binaryPath) when set. The default ignores it.</summary>
    Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, string? configuredPath, CancellationToken ct)
        => GetBinaryPathAsync(extended, allowDownload, ct);
}
