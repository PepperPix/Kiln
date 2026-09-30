namespace Kiln.Services;

public interface IPagefindBinaryProvider
{
    Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, CancellationToken ct);

    /// <summary>Resolves the binary, preferring <paramref name="configuredPath"/> (search.binaryPath) when set. The default ignores it.</summary>
    Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, string? configuredPath, CancellationToken ct)
        => GetBinaryPathAsync(extended, allowDownload, ct);
}
