namespace Kiln.Services;

public interface IPagefindBinaryProvider
{
    Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, CancellationToken ct);
}
