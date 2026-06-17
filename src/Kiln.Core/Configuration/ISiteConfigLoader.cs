namespace Kiln.Services;

using Kiln.Models;

public interface ISiteConfigLoader
{
    SiteConfiguration Load(string projectPath);
}
