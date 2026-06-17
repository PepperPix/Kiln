namespace Kiln.Services;

using Kiln.Models;

public interface ISiteBuilder
{
    /// <summary>
    /// Builds the entire site from source to output directory.
    /// </summary>
    Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default);
}
