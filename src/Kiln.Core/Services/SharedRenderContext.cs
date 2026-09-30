namespace Kiln.Services;

using Kiln.Models;
using Scriban.Runtime;

/// <summary>
/// The render data that is shared by all pages of a build and passed to <see cref="ITemplateRenderer"/>.
/// </summary>
public sealed class SharedRenderContext
{
    /// <summary>Gets the site-level values that templates read through <c>site</c>.</summary>
    public required object Site { get; init; }

    /// <summary>Gets the template representation of each collection, keyed by collection name. Drafts are not included.</summary>
    public required IReadOnlyDictionary<string, object> Collections { get; init; }

    /// <summary>Gets the taxonomies with their terms, keyed by taxonomy name.</summary>
    public required ScriptObject Taxonomies { get; init; }

    /// <summary>Gets the site-level plugin settings, keyed by plugin name.</summary>
    public required IReadOnlyDictionary<string, object> Plugins { get; init; }

    /// <summary>Gets the theme settings from <see cref="SiteConfiguration.ThemeConfig"/>.</summary>
    public required IReadOnlyDictionary<string, object> Theme { get; init; }

    /// <summary>
    /// Pre-built navigation tree per collection (raw, without is_active/is_ancestor flags).
    /// Flags are added per render in the template renderer.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<NavigationNode>> NavTree { get; init; }

    /// <summary>
    /// Creates the shared render context of a build.
    /// </summary>
    /// <param name="site">The site configuration.</param>
    /// <param name="allTaxonomies">The terms of each taxonomy, keyed by taxonomy name.</param>
    /// <param name="navTree">The navigation tree of each collection, or <see langword="null"/> for none.</param>
    /// <returns>The shared render context.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="site"/> or <paramref name="allTaxonomies"/> is <see langword="null"/>.</exception>
    public static SharedRenderContext Build(
        SiteConfiguration site,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        IReadOnlyDictionary<string, IReadOnlyList<NavigationNode>>? navTree = null)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(allTaxonomies);

        var collections = site.Collections.ToDictionary(
            kvp => kvp.Key,
            kvp => BuildCollectionObject(kvp.Value, site.BasePath));

        var taxonomies = new ScriptObject();
        foreach (var (name, termList) in allTaxonomies)
        {
            var taxTerms = termList.Select(t => (object)new
            {
                name = t.Name,
                slug = t.Slug,
                url = SiteConfiguration.ApplyBasePath(site.BasePath, t.Url),
                count = t.Count,
                items = t.Items.Select(item => BuildItemSummary(item, site.BasePath)).ToList()
            }).ToList();

            taxonomies.Add(name, new { terms = taxTerms });
        }

        return new SharedRenderContext
        {
            Site = new
            {
                title = site.Title,
                description = site.Description,
                base_url = site.BaseUrl.ToString().TrimEnd('/'),
                origin = site.Origin,
                base_path = site.BasePath,
                language = site.Language,
                asset_prefix = site.AssetPrefix,
                search = new { enabled = site.Search.Enabled }
            },
            Collections = collections,
            Taxonomies = taxonomies,
            Plugins = new Dictionary<string, object>(site.Plugins),
            Theme = new Dictionary<string, object>(site.ThemeConfig),
            NavTree = navTree ?? new Dictionary<string, IReadOnlyList<NavigationNode>>()
        };
    }

    internal static object BuildCollectionObject(ContentGroup collection, string basePath = "")
    {
        return new
        {
            name = collection.Name,
            items = collection.Items.Where(static i => !i.Draft).Select(item => BuildItemSummary(item, basePath)).ToList(),
            url = SiteConfiguration.ApplyBasePath(basePath, collection.IndexUrl),
            feed = collection.Feed,
            plugins = collection.Plugins
        };
    }

    internal static object BuildItemSummary(ContentItem item, string basePath = "")
    {
        return new
        {
            title = item.Title,
            url = SiteConfiguration.ApplyBasePath(basePath, item.Url),
            slug = item.Slug,
            date = item.Date,
            description = item.Description,
            teaser = item.Teaser,
            draft = item.Draft,
            extra = item.Extra,
            tags = item.Taxonomies.GetValueOrDefault("tags"),
            categories = item.Taxonomies.GetValueOrDefault("categories"),
            taxonomies = item.Taxonomies
        };
    }
}
