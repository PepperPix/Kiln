namespace Kiln.Models;

#pragma warning disable CA1002 // Builder pattern needs mutable list

public sealed class NavigationNode
{
    public required string Title { get; init; }
    public required Uri Url { get; init; }
    public int Weight { get; init; }
    public List<NavigationNode> Children { get; init; } = [];
}

#pragma warning restore CA1002
