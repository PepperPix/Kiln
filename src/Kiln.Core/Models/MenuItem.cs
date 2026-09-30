namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// An entry of a <see cref="Menu"/>, which can contain child entries.
/// </summary>
public sealed class MenuItem
{
    /// <summary>Gets the display title of the entry.</summary>
    public required string Title { get; init; }

    /// <summary>Gets or sets the URL of the entry. For entries with a <see cref="Ref"/>, the builder resolves it during a build.</summary>
    public Uri? Url { get; set; }

    /// <summary>Gets the raw reference of the entry: <c>collection/slug</c> for an item, or <c>collection/</c> for the index of a collection.</summary>
    public string? Ref { get; init; }

    /// <summary>Gets whether the entry points to an external target. The URL of an external entry is not checked against the pages of the site.</summary>
    public bool External { get; init; }

    /// <summary>Gets or sets whether the entry is marked as active.</summary>
    public bool Active { get; set; }

    /// <summary>Gets the child entries.</summary>
    public Collection<MenuItem> Children { get; init; } = [];
}
