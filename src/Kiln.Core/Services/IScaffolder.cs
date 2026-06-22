namespace Kiln.Services;

using Kiln.Models;

public interface IScaffolder
{
    ScaffoldResult CreateSite(string name, string outputDirectory, CancellationToken cancellationToken = default);
}
