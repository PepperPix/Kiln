namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// A single term of a taxonomy together with the items that use it.
/// </summary>
public sealed class TaxonomyTerm
{
    /// <summary>Gets the name of the term.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the slug of the term.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the taxonomy the term belongs to.</summary>
    public required TaxonomyDefinition Taxonomy { get; init; }

    /// <summary>Gets the URL of the term page.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the items that use the term.</summary>
    public Collection<ContentItem> Items { get; } = [];

    /// <summary>Gets the number of items that use the term.</summary>
    public int Count => Items.Count;
}
