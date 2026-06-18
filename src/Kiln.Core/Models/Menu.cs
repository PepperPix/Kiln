namespace Kiln.Models;

using System.Collections.ObjectModel;

public sealed class Menu
{
    public required string Name { get; init; }
    public required Collection<MenuItem> Items { get; init; }
}
