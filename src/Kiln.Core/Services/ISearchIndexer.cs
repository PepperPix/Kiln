namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Builds the search index of a generated site.
/// </summary>
public interface ISearchIndexer
{
    /// <summary>
    /// Builds the search index for the site in the output directory.
    /// </summary>
    /// <param name="outputDir">The output directory of the site.</param>
    /// <param name="options">The search settings.</param>
    /// <param name="allowDownload">Whether the indexer executable may be downloaded when it cannot be found locally.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The result of the indexing run; failures are reported in the result.</returns>
    Task<SearchIndexResult> IndexAsync(
        string outputDir,
        SearchOptions options,
        bool allowDownload,
        CancellationToken ct);
}
