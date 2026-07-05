namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeOpenApiDocGenerator : IOpenApiDocGenerator
{
    public Func<DocGenReport>? ResultFactory { get; set; }

    public DocGenReport Generate(string specPath, string outputDir) =>
        ResultFactory?.Invoke() ?? new DocGenReport([], [], [], []);
}
