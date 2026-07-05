namespace Kiln.Cli.Tests.Fakes;

using Kiln.Abstractions;
using Kiln.Models;
using Kiln.Services;

public sealed class FakeSiteBuilder : ISiteBuilder
{
    public bool? CapturedIncludeDrafts { get; private set; }

    public BuildEnvironment? CapturedEnvironment { get; private set; }

    public Func<BuildResult>? ResultFactory { get; set; }

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default) =>
        BuildAsync(projectPath, includeDrafts, BuildEnvironment.Development, ct);

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, CancellationToken ct)
    {
        CapturedIncludeDrafts = includeDrafts;
        CapturedEnvironment = environment;

        var result = ResultFactory?.Invoke() ?? new BuildResult
        {
            TotalFiles = 1,
            RenderedFiles = 1,
            SkippedDrafts = 0,
            Duration = TimeSpan.Zero,
            OutputDirectory = "_site",
        };

        return Task.FromResult(result);
    }
}
