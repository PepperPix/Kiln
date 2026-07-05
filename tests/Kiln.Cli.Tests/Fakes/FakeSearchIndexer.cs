namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeSearchIndexer : ISearchIndexer
{
    public bool WasCalled { get; private set; }

    public string? CapturedOutputDir { get; private set; }

    public SearchOptions? CapturedOptions { get; private set; }

    public bool? CapturedAllowDownload { get; private set; }

    public Func<SearchIndexResult>? ResultFactory { get; set; }

    public Task<SearchIndexResult> IndexAsync(
        string outputDir,
        SearchOptions options,
        bool allowDownload,
        CancellationToken ct)
    {
        WasCalled = true;
        CapturedOutputDir = outputDir;
        CapturedOptions = options;
        CapturedAllowDownload = allowDownload;

        var result = ResultFactory?.Invoke() ?? new SearchIndexResult(true, [], []);
        return Task.FromResult(result);
    }
}
