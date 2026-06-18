namespace Kiln.Models;

using System.Collections.ObjectModel;

public sealed class TaxonomyTerm
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required TaxonomyDefinition Taxonomy { get; init; }
    public required Uri Url { get; init; }
    public Collection<ContentItem> Items { get; } = [];
    public int Count => Items.Count;
}
