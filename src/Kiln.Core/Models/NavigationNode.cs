namespace Kiln.Models;

public sealed class NavigationNode
{
    public required string Title { get; init; }
    public required Uri Url { get; init; }
    public int Weight { get; init; }
    public List<NavigationNode> Children { get; init; } = [];
}
