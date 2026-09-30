namespace Kiln.Models;

/// <summary>
/// Selects what is rendered at the site root, read from the <c>home</c> section of <c>site.yaml</c>.
/// </summary>
public sealed class HomeConfiguration
{
    /// <summary>Path to a standalone markdown file rendered at "/". Mutually exclusive with Collection.</summary>
    public string? Page { get; init; }

    /// <summary>Name of a collection whose index is promoted to "/". Mutually exclusive with Page.</summary>
    public string? Collection { get; init; }
}