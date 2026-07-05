namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeOpenApiDocGenerator : IOpenApiDocGenerator
{
    public string? CapturedSpecPath { get; private set; }

    public string? CapturedOutputDir { get; private set; }

    public Func<DocGenReport>? ResultFactory { get; set; }

    public DocGenReport Generate(string specPath, string outputDir)
    {
        CapturedSpecPath = specPath;
        CapturedOutputDir = outputDir;

        return ResultFactory?.Invoke() ?? new DocGenReport([], [], [], []);
    }
}
