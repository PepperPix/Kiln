namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// A named collection of content items (for example posts or pages), configured in the <c>collections</c> section of <c>site.yaml</c>.
/// </summary>
public sealed class ContentGroup
{
    /// <summary>
    /// Default number of words used for the automatic teaser truncation fallback (WordPress convention).
    /// </summary>
    public const int DefaultTeaserWords = 55;

    /// <summary>Gets the name of the collection, which is its key in the site configuration.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the directory containing the Markdown files of the collection. Relative paths are resolved against the project directory.</summary>
    public string Directory { get; init; } = "";

    /// <summary>
    /// Gets the permalink pattern of the items. The tokens <c>:slug</c>, <c>:collection</c>, <c>:year</c>,
    /// <c>:month</c> and <c>:day</c> are replaced; the date tokens require an item date. The default is <c>/:slug/</c>.
    /// </summary>
    public string Permalink { get; init; } = "/:slug/";

    /// <summary>
    /// Gets the sort order applied when the items are read: <c>date desc</c>, <c>date asc</c>, <c>title asc</c> or <c>weight asc</c>.
    /// Any other value, including the default <c>none</c>, keeps the order in which the files are discovered.
    /// </summary>
    public string Sort { get; init; } = "none";

    /// <summary>Gets whether a feed is generated for the collection.</summary>
    public bool Feed { get; init; }

    /// <summary>Gets the number of items per index page. When unset or not positive, no index pages are rendered for the collection.</summary>
    public int? Paginate { get; init; }

    /// <summary>Gets the name of the layout used for items that do not set their own <see cref="ContentItem.Layout"/>. The default is <c>default</c>.</summary>
    public string Layout { get; init; } = "default";

    /// <summary>Gets the maximum number of words of the teaser that is derived from the body when an item has neither a description nor a <c>&lt;!--more--&gt;</c> marker.</summary>
    public int TeaserWords { get; init; } = DefaultTeaserWords;

    /// <summary>Gets the names of the taxonomies that apply to the items of the collection.</summary>
    public Collection<string> Taxonomies { get; init; } = [];

    /// <summary>Gets the cross-collection references, mapping an item's front matter key to the name of the collection whose item slug the value refers to.</summary>
    public Dictionary<string, string> References { get; init; } = [];

    /// <summary>Gets the per-collection plugin settings, keyed by plugin name.</summary>
    public Dictionary<string, object> Plugins { get; init; } = [];

    /// <summary>Gets the custom values from the <c>extra</c> section of the collection.</summary>
    public Dictionary<string, object> Extra { get; init; } = [];

    // Populated during build

    /// <summary>Gets the items of the collection. The builder fills this collection during a build.</summary>
    public Collection<ContentItem> Items { get; } = [];

    /// <summary>Gets or sets a URL that replaces the derived <see cref="IndexUrl"/>. The builder sets it when the collection is promoted to the site root.</summary>
    public Uri? IndexUrlOverride { get; set; }

    /// <summary>Gets the relative URL of the collection, <c>/{Name}/</c>. This is the initial <see cref="ContentItem.Url"/> of its items before the permalink is applied.</summary>
    public Uri Url
    {
        get
        {
            var sep = Path.AltDirectorySeparatorChar;
            return new Uri($"{sep}{Name}{sep}", UriKind.Relative);
        }
    }

    /// <summary>
    /// The URL of the collection's index page, derived from the Permalink pattern.
    /// E.g. permalink "/blog/:slug/" → IndexUrl "/blog/".
    /// </summary>
    public Uri IndexUrl
    {
        get
        {
            if (IndexUrlOverride is not null)
                return IndexUrlOverride;

            var slugPos = Permalink.IndexOf(":slug", StringComparison.OrdinalIgnoreCase);
            var path = slugPos < 0 ? $"/{Name}/" : Permalink[..slugPos];
            return new Uri(path, UriKind.Relative);
        }
    }
}
