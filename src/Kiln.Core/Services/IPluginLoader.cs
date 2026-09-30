namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Discovers the plugins of a site.
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// Loads the plugins found in the <c>plugins</c> directory of a project. Each subdirectory with a
    /// <c>plugin.yaml</c> (or <c>plugin.yml</c>) manifest is one plugin.
    /// </summary>
    /// <param name="projectPath">The project directory.</param>
    /// <returns>The plugins ordered by directory name; empty when there is no <c>plugins</c> directory.</returns>
    /// <exception cref="InvalidOperationException">A plugin manifest is not valid YAML.</exception>
    IReadOnlyList<PluginDefinition> LoadPlugins(string projectPath);
}
