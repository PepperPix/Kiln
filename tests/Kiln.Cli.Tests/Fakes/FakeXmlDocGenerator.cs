namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeXmlDocGenerator : IXmlDocGenerator
{
    public Func<DocGenReport>? ResultFactory { get; set; }

    public DocGenReport Generate(string xmlDocPath, string outputDir) =>
        ResultFactory?.Invoke() ?? new DocGenReport([], [], [], []);
}
