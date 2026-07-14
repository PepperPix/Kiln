namespace Kiln.Cli.Tests.Fakes;

using Kiln.Abstractions;
using Kiln.Models;
using Kiln.Services;

public sealed class FakeSiteBuilder : ISiteBuilder
{
    public bool? CapturedIncludeDrafts { get; private set; }

    public BuildEnvironment? CapturedEnvironment { get; private set; }

    public Func<BuildResult>? ResultFactory { get; set; }

    public IProgress<BuildProgress>? CapturedProgress { get; private set; }

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default) =>
        BuildAsync(projectPath, includeDrafts, BuildEnvironment.Development, progress: null, ct);

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, CancellationToken ct) =>
        BuildAsync(projectPath, includeDrafts, environment, progress: null, ct);

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, IProgress<BuildProgress>? progress, CancellationToken ct)
    {
        CapturedIncludeDrafts = includeDrafts;
        CapturedEnvironment = environment;
        CapturedProgress = progress;

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
