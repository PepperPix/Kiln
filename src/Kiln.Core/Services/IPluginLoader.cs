namespace Kiln.Services;

using Kiln.Models;

public interface IPluginLoader
{
    IReadOnlyList<PluginDefinition> LoadPlugins(string projectPath);
}
