namespace Kiln.Models;

/// <summary>
/// A node of the navigation tree of a collection. Nodes are built from item titles and from the directories
/// (sections) the items reside in.
/// </summary>
public sealed class NavigationNode
{
    /// <summary>Gets the display title of the node.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the relative URL of the node, including the base path of the site.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the weight used for ordering siblings.</summary>
    public int Weight { get; init; }

    /// <summary>Gets the child nodes.</summary>
    public List<NavigationNode> Children { get; init; } = [];
}
