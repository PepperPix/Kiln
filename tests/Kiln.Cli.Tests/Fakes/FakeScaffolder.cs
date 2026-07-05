namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeScaffolder : IScaffolder
{
    public Func<ScaffoldResult>? ResultFactory { get; set; }

    public Exception? ThrowException { get; set; }

    public ScaffoldResult CreateSite(string name, string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (ThrowException is not null)
            throw ThrowException;

        return ResultFactory?.Invoke() ?? new ScaffoldResult(Path.Combine(outputDirectory, name), []);
    }
}
