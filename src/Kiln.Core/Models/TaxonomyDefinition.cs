namespace Kiln.Models;

/// <summary>
/// A taxonomy (for example tags or categories), configured in the <c>taxonomies</c> section of <c>site.yaml</c>.
/// </summary>
public sealed class TaxonomyDefinition
{
    /// <summary>Gets the name of the taxonomy, which is its key in the site configuration.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the permalink pattern of the term pages, in which <c>:slug</c> is replaced by the slug of the term. The default is <c>/:slug/</c>.</summary>
    public string Permalink { get; init; } = "/:slug/";

    /// <summary>Gets the number of items per term page. When unset or not positive, term pages are not paginated.</summary>
    public int? Paginate { get; init; }
}
