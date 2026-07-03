namespace Kiln.Services;

using Kiln.Models;

public interface IXmlDocGenerator
{
    DocGenReport Generate(string xmlDocPath, string outputDir);
}
