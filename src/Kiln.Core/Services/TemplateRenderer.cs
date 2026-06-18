namespace Kiln.Services;

using System.Linq;
using System.Text.RegularExpressions;
using Kiln.Models;
using Scriban;
using Scriban.Runtime;

public sealed class TemplateRenderer : ITemplateRenderer
{
    public string Render(ContentItem item, SiteConfiguration site, string themePath)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(site);

        var layoutName = item.Layout ?? item.Collection.Layout;
        var layoutPath = ResolveLayoutPath(themePath,
            $"{layoutName}.html",
            "default.html");

        return RenderTemplate(layoutPath, themePath, site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(), ctx =>
            BuildItemScriptObject(item, site, themePath, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(), ctx));
    }

    public string RenderCollectionIndex(
        ContentGroup collection,
        Paginator paginator,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        SiteConfiguration site,
        string themePath)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(paginator);
        ArgumentNullException.ThrowIfNull(allTaxonomies);
        ArgumentNullException.ThrowIfNull(site);

        var layoutPath = ResolveLayoutPath(themePath,
            $"{collection.Name}-index.html",
            "index.html",
            "default.html");

        return RenderTemplate(layoutPath, themePath, site, allTaxonomies, ctx =>
        {
            var so = BuildCommonScriptObject(site, allTaxonomies, themePath, ctx);
            so.Add("collection", BuildCollectionObject(collection));
            so.Add("paginator", BuildPaginatorObject(paginator));
            return so;
        });
    }

    public string RenderTaxonomyTerm(
        TaxonomyTerm term,
        Paginator paginator,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        SiteConfiguration site,
        string themePath)
    {
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(paginator);
        ArgumentNullException.ThrowIfNull(allTaxonomies);
        ArgumentNullException.ThrowIfNull(site);

        var layoutPath = ResolveLayoutPath(themePath,
            $"taxonomy-{term.Taxonomy.Name}.html",
            "taxonomy.html",
            "default.html");

        return RenderTemplate(layoutPath, themePath, site, allTaxonomies, ctx =>
        {
            var so = BuildCommonScriptObject(site, allTaxonomies, themePath, ctx);
            so.Add("taxonomy", new
            {
                name = term.Taxonomy.Name,
                term = term.Name,
                url = term.Url.OriginalString,
                items = term.Items.Select(BuildItemSummary).ToList()
            });
            so.Add("paginator", BuildPaginatorObject(paginator));
            return so;
        });
    }

    public string RenderTaxonomyOverview(
        TaxonomyDefinition taxonomy,
        IReadOnlyList<TaxonomyTerm> terms,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        SiteConfiguration site,
        string themePath)
    {
        ArgumentNullException.ThrowIfNull(taxonomy);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(allTaxonomies);
        ArgumentNullException.ThrowIfNull(site);

        var overviewUrl = GetTaxonomyOverviewUrl(taxonomy);

        var layoutPath = ResolveLayoutPath(themePath,
            $"taxonomy-{taxonomy.Name}-index.html",
            "taxonomy-index.html",
            "default.html");

        return RenderTemplate(layoutPath, themePath, site, allTaxonomies, ctx =>
        {
            var so = BuildCommonScriptObject(site, allTaxonomies, themePath, ctx);
            so.Add("taxonomy", new
            {
                name = taxonomy.Name,
                url = overviewUrl.OriginalString,
                terms = terms.Select(t => new
                {
                    name = t.Name,
                    slug = t.Slug,
                    url = t.Url.OriginalString,
                    count = t.Count
                }).ToList()
            });
            return so;
        });
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static string RenderTemplate(
        string layoutPath,
        string themePath,
        SiteConfiguration site,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        Func<TemplateContext, ScriptObject> buildScriptObject)
    {
        var templateSource = File.ReadAllText(layoutPath);
        var template = Template.Parse(templateSource, layoutPath);

        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Template errors in '{layoutPath}': {string.Join(", ", template.Messages)}");

        var context = new TemplateContext();
        var scriptObject = buildScriptObject(context);
        context.PushGlobal(scriptObject);
        return template.Render(context);
    }

    private static ScriptObject BuildItemScriptObject(
        ContentItem item,
        SiteConfiguration site,
        string themePath,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        TemplateContext context)
    {
        var so = BuildCommonScriptObject(site, allTaxonomies, themePath, context);

        var pageObj = new ScriptObject();
        pageObj.Add("id", item.Id);
        pageObj.Add("title", item.Title);
        pageObj.Add("date", item.Date);
        pageObj.Add("content", item.HtmlContent);
        pageObj.Add("url", item.Url.OriginalString);
        pageObj.Add("slug", item.Slug);
        pageObj.Add("description", item.Description);
        pageObj.Add("draft", item.Draft);
        pageObj.Add("weight", item.Weight);
        pageObj.Add("extra", item.Extra);
        pageObj.Add("tags", item.Taxonomies.GetValueOrDefault("tags"));
        pageObj.Add("categories", item.Taxonomies.GetValueOrDefault("categories"));
        pageObj.Add("collection", new { name = item.Collection.Name, url = item.Collection.IndexUrl.OriginalString });
        pageObj.Add("next", item.Next is null ? null : (object)new
        {
            title = item.Next.Title,
            url = item.Next.Url.OriginalString
        });
        pageObj.Add("prev", item.Prev is null ? null : (object)new
        {
            title = item.Prev.Title,
            url = item.Prev.Url.OriginalString
        });

        foreach (var (refKey, refItem) in item.ResolvedReferences)
        {
            pageObj.Add(refKey, new
            {
                title = refItem.Title,
                url = refItem.Url.OriginalString,
                slug = refItem.Slug,
                extra = refItem.Extra
            });
        }

        so.Add("page", pageObj);

        // Co-located asset_url for page bundles
        var assetPrefix = site.AssetPrefix.TrimEnd('/');
        so.Import("page_asset_url", new Func<string, string>(
            filename => $"{assetPrefix}/content/{item.Collection.Name}/{item.Slug}/{filename.TrimStart('/')}"));

        return so;
    }

    private static ScriptObject BuildCommonScriptObject(
        SiteConfiguration site,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        string themePath,
        TemplateContext context)
    {
        var so = new ScriptObject();

        so.Add("site", new
        {
            title = site.Title,
            description = site.Description,
            base_url = site.BaseUrl.ToString().TrimEnd('/'),
            language = site.Language,
            asset_prefix = site.AssetPrefix
        });

        var collectionsDict = site.Collections.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)BuildCollectionObject(kvp.Value));
        so.Add("collections", collectionsDict);
        so.Add("plugins", site.Plugins);
        so.Add("theme", site.ThemeConfig);

        // taxonomies global object available in all templates
        var taxonomiesObj = new ScriptObject();
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
            taxonomiesObj.Add(name, new { terms = taxTerms });
        }
        so.Add("taxonomies", taxonomiesObj);

        // include partial
        var partialsDir = Path.Combine(themePath, "partials");
        so.Import("include", new Func<string, string>(partialName =>
        {
            var partialPath = Path.Combine(partialsDir, $"{partialName}.html");
            if (!File.Exists(partialPath))
                return $"<!-- partial '{partialName}' not found -->";
            var partialTemplate = Template.Parse(File.ReadAllText(partialPath), partialPath);
            return partialTemplate.Render(context);
        }));

        // asset_url
        var assetPrefix = site.AssetPrefix.TrimEnd('/');
        so.Import("asset_url", new Func<string, string>(
            path => $"{assetPrefix}/{path.TrimStart('/')}"));

        return so;
    }

    private static object BuildCollectionObject(ContentGroup collection)
    {
        return new
        {
            name = collection.Name,
            items = collection.Items.Where(static i => !i.Draft).Select(BuildItemSummary).ToList(),
            url = collection.IndexUrl.OriginalString
        };
    }

    private static object BuildPaginatorObject(Paginator paginator)
    {
        return new
        {
            items = paginator.Items.Select(BuildItemSummary).ToList(),
            page = paginator.Page,
            total_pages = paginator.TotalPages,
            total_items = paginator.TotalItems,
            next_url = paginator.NextUrl?.OriginalString,
            prev_url = paginator.PrevUrl?.OriginalString
        };
    }

    private static object BuildItemSummary(ContentItem item)
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

    private static string ResolveLayoutPath(string themePath, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(themePath, "layouts", candidate);
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException(
            $"No layout found in '{themePath}/layouts/'. Tried: {string.Join(", ", candidates)}");
    }

    internal static Uri GetTaxonomyOverviewUrl(TaxonomyDefinition def)
    {
        var slugPos = def.Permalink.IndexOf(":slug", StringComparison.OrdinalIgnoreCase);
        var path = slugPos < 0 ? $"/{def.Name}/" : def.Permalink[..slugPos];
        return new Uri(path, UriKind.Relative);
    }

    internal static string ToSlug(string text)
    {
#pragma warning disable CA1308 // Slug normalisation requires lowercase, not uppercase
        var lower = text.ToLowerInvariant();
#pragma warning restore CA1308
        var result = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }
}

