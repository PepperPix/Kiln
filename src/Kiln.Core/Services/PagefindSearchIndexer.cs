namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Default <see cref="ISearchIndexer"/> implementation that runs Pagefind against the output directory.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on <see cref="ISearchIndexer"/> instead.
/// </remarks>
public sealed class PagefindSearchIndexer(
    IPagefindBinaryProvider binaryProvider,
    IProcessRunner processRunner) : ISearchIndexer
{
    public Task<SearchIndexResult> IndexAsync(
        string outputDir,
        SearchOptions options,
        bool allowDownload,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        return IndexCoreAsync(outputDir, options, allowDownload, ct);
    }

    private async Task<SearchIndexResult> IndexCoreAsync(
        string outputDir,
        SearchOptions options,
        bool allowDownload,
        CancellationToken ct)
    {
        string binaryPath;
        try
        {
            binaryPath = await binaryProvider
                .GetBinaryPathAsync(options.Extended, allowDownload, options.BinaryPath, ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return new SearchIndexResult(false, [], [ex.Message]);
        }
        catch (PlatformNotSupportedException ex)
        {
            return new SearchIndexResult(false, [], [ex.Message]);
        }

        ProcessRunResult result;
        try
        {
            result = await processRunner
                .RunAsync(binaryPath, $"--site {ProcessArguments.Quote(outputDir)}", null, ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return new SearchIndexResult(false, [], [$"Failed to run Pagefind: {ex.Message}"]);
        }

        if (result.ExitCode == 0)
            return new SearchIndexResult(true, [], []);

        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.StdErr))
            errors.Add(result.StdErr.Trim());
        if (!string.IsNullOrWhiteSpace(result.StdOut))
            errors.Add(result.StdOut.Trim());
        if (errors.Count == 0)
            errors.Add($"Pagefind exited with code {result.ExitCode}");

        return new SearchIndexResult(false, [], errors);
    }
}
