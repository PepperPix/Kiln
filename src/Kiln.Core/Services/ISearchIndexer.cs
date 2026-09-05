namespace Kiln.Services;

using Kiln.Models;

public interface ISearchIndexer
{
    Task<SearchIndexResult> IndexAsync(
        string outputDir,
        SearchOptions options,
        bool allowDownload,
        CancellationToken ct);
}
