namespace Kiln.Services;

using Kiln.Models;

public interface IOpenApiDocGenerator
{
    DocGenReport Generate(string specPath, string outputDir);
}
