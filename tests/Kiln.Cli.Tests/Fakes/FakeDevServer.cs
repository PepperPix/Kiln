namespace Kiln.Cli.Tests.Fakes;

using Kiln.Services;

public sealed class FakeDevServer : IDevServer
{
    public string? CapturedProjectPath { get; private set; }

    public int? CapturedPort { get; private set; }

    public bool? CapturedIncludeDrafts { get; private set; }

    public Func<Task>? RunBehavior { get; set; }

    public Task RunAsync(string projectPath, int port = 5555, bool includeDrafts = false, CancellationToken ct = default)
    {
        CapturedProjectPath = projectPath;
        CapturedPort = port;
        CapturedIncludeDrafts = includeDrafts;

        return RunBehavior?.Invoke() ?? Task.CompletedTask;
    }
}
