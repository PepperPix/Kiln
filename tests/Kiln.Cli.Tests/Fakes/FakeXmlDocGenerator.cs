namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeXmlDocGenerator : IXmlDocGenerator
{
    public string? CapturedXmlPath { get; private set; }

    public string? CapturedOutputDir { get; private set; }

    public Func<DocGenReport>? ResultFactory { get; set; }

    public DocGenReport Generate(string xmlDocPath, string outputDir)
    {
        CapturedXmlPath = xmlDocPath;
        CapturedOutputDir = outputDir;

        return ResultFactory?.Invoke() ?? new DocGenReport([], [], [], []);
    }
}
