namespace Kiln.Services;

using Kiln.Abstractions;
using Kiln.Models;

public interface ISiteBuilder
{
    /// <summary>
    /// Builds the entire site from source to output directory (development environment).
    /// </summary>
    Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default);

    /// <summary>
    /// Builds the entire site for the specified build environment.
    /// </summary>
    Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, CancellationToken ct);

    /// <summary>
    /// Builds the entire site for the specified build environment and reports progress.
    /// </summary>
    Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, IProgress<BuildProgress>? progress, CancellationToken ct);

    /// <summary>
    /// Builds the entire site for the specified build environment, reports progress, and
    /// optionally overrides the site's configured base URL for this run.
    /// </summary>
    Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, IProgress<BuildProgress>? progress, Uri? baseUrlOverride, CancellationToken ct = default);
}
