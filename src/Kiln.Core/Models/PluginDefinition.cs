namespace Kiln.Models;

using System.Collections.ObjectModel;

public sealed class PluginDefinition
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? Description { get; init; }
    public Collection<string> Slots { get; init; } = [];
    public string Directory { get; init; } = "";
}
