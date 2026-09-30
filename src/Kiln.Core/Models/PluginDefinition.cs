namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// A plugin found in the <c>plugins</c> directory of a site, described by its <c>plugin.yaml</c> manifest.
/// </summary>
public sealed class PluginDefinition
{
    /// <summary>Gets the name of the plugin. Without a name in the manifest, this is the name of the plugin directory.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the version from the manifest, if any.</summary>
    public string? Version { get; init; }

    /// <summary>Gets the description from the manifest, if any.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the names of the template slots the plugin provides content for.</summary>
    public Collection<string> Slots { get; init; } = [];

    /// <summary>Gets the names of the shortcodes the plugin provides.</summary>
    public Collection<string> Shortcodes { get; init; } = [];

    /// <summary>Gets the path of the plugin directory.</summary>
    public string Directory { get; init; } = "";
}
