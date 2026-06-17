namespace Kiln.Services;

public interface IDevServer
{
    /// <summary>
    /// Starts a local HTTP server serving the output directory, with file watching and auto-rebuild.
    /// </summary>
    Task RunAsync(string projectPath, int port = 5555, bool includeDrafts = false, CancellationToken ct = default);
}
