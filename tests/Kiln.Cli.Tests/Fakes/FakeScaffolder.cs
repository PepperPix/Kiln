namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeScaffolder : IScaffolder
{
    public string? CapturedName { get; private set; }

    public string? CapturedOutputDirectory { get; private set; }

    public Func<ScaffoldResult>? ResultFactory { get; set; }

    public Exception? ThrowException { get; set; }

    public ScaffoldResult CreateSite(string name, string outputDirectory, CancellationToken cancellationToken = default)
    {
        CapturedName = name;
        CapturedOutputDirectory = outputDirectory;

        if (ThrowException is not null)
            throw ThrowException;

        return ResultFactory?.Invoke() ?? new ScaffoldResult(Path.Combine(outputDirectory, name), []);
    }
}
