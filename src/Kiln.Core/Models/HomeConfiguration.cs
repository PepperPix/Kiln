namespace Kiln.Models;

public sealed class HomeConfiguration
{
    /// <summary>Path to a standalone markdown file rendered at "/". Mutually exclusive with Collection.</summary>
    public string? Page { get; init; }

    /// <summary>Name of a collection whose index is promoted to "/". Mutually exclusive with Page.</summary>
    public string? Collection { get; init; }
}