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
}
