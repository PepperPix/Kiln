namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// A named navigation menu, configured in the <c>menus</c> section of <c>site.yaml</c>.
/// </summary>
public sealed class Menu
{
    /// <summary>Gets the name of the menu, which is its key in the site configuration.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the top-level items of the menu.</summary>
    public required Collection<MenuItem> Items { get; init; }
}
