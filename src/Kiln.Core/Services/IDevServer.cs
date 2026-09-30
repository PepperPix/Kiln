namespace Kiln.Services;

/// <summary>
/// Serves a site locally during development.
/// </summary>
public interface IDevServer
{
    /// <summary>
    /// Starts a local HTTP server serving the output directory, with file watching and auto-rebuild.
    /// </summary>
    /// <param name="projectPath">The project directory.</param>
    /// <param name="port">The port to listen on.</param>
    /// <param name="includeDrafts">Whether drafts are included in the builds.</param>
    /// <param name="ct">A token to stop the server.</param>
    /// <returns>A task that completes when the server has stopped.</returns>
    /// <exception cref="InvalidOperationException">The initial build failed.</exception>
    Task RunAsync(string projectPath, int port = 5555, bool includeDrafts = false, CancellationToken ct = default);
}
