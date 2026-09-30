namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeXmlDocGenerator : IXmlDocGenerator
{
    public Func<DocGenReport>? ResultFactory { get; set; }

    public Exception? ExceptionToThrow { get; set; }

    public XmlDocGenerationOptions? LastOptions { get; private set; }

    public DocGenReport Generate(string xmlDocPath, string outputDir) =>
        ResultFactory?.Invoke() ?? new DocGenReport([], [], [], []);

    public DocGenReport Generate(string xmlDocPath, string outputDir, XmlDocGenerationOptions options)
    {
        LastOptions = options;
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;

        return Generate(xmlDocPath, outputDir);
    }
}
