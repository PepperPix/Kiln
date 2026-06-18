namespace Kiln.Models;

public sealed class TaxonomyDefinition
{
    public required string Name { get; init; }
    public string Permalink { get; init; } = "/:slug/";
    public int? Paginate { get; init; }
}
