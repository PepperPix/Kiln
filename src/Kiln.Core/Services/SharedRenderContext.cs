namespace Kiln.Services;

using Kiln.Models;
using Scriban.Runtime;

public sealed class SharedRenderContext
{
    public required object Site { get; init; }
    public required IReadOnlyDictionary<string, object> Collections { get; init; }
    public required ScriptObject Taxonomies { get; init; }
    public required IReadOnlyDictionary<string, object> Plugins { get; init; }
    public required IReadOnlyDictionary<string, object> Theme { get; init; }

    public static SharedRenderContext Build(
        SiteConfiguration site,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(allTaxonomies);

        var collections = site.Collections.ToDictionary(
            kvp => kvp.Key,
            kvp => BuildCollectionObject(kvp.Value));

        var taxonomies = new ScriptObject();
        foreach (var (name, termList) in allTaxonomies)
        {
            var taxTerms = termList.Select(t => (object)new
            {
                name = t.Name,
                slug = t.Slug,
                url = t.Url.OriginalString,
                count = t.Count,
                items = t.Items.Select(BuildItemSummary).ToList()
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
                language = site.Language,
                asset_prefix = site.AssetPrefix
            },
            Collections = collections,
            Taxonomies = taxonomies,
            Plugins = new Dictionary<string, object>(site.Plugins),
            Theme = new Dictionary<string, object>(site.ThemeConfig)
        };
    }

    internal static object BuildCollectionObject(ContentGroup collection)
    {
        return new
        {
            name = collection.Name,
            items = collection.Items.Where(static i => !i.Draft).Select(BuildItemSummary).ToList(),
            url = collection.IndexUrl.OriginalString,
            feed = collection.Feed,
            plugins = collection.Plugins
        };
    }

    internal static object BuildItemSummary(ContentItem item)
    {
        return new
        {
            title = item.Title,
            url = item.Url.OriginalString,
            slug = item.Slug,
            date = item.Date,
            description = item.Description,
            draft = item.Draft,
            extra = item.Extra,
            tags = item.Taxonomies.GetValueOrDefault("tags"),
            categories = item.Taxonomies.GetValueOrDefault("categories")
        };
    }
}