namespace Kiln.Cli.Tests.Fakes;

using Kiln.Services;

public sealed class FakeDevServer : IDevServer
{
    public Func<Task>? RunBehavior { get; set; }

    public Task RunAsync(string projectPath, int port = 5555, bool includeDrafts = false, CancellationToken ct = default) =>
        RunBehavior?.Invoke() ?? Task.CompletedTask;
}
