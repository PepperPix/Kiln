namespace Kiln.Services;

using System.Linq;
using System.Text.RegularExpressions;
using Kiln.Models;
using Scriban;
using Scriban.Runtime;

public sealed class TemplateRenderer : ITemplateRenderer
{
    public string Render(ContentItem item, SiteConfiguration site, string themePath, IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var layoutName = item.Layout ?? item.Collection.Layout;
        var layoutPath = ResolveLayoutPath(themePath,
            $"{layoutName}.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
            BuildItemScriptObject(item, site, themePath, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(), ctx, plugins));
    }

    public string RenderCollectionIndex(
        ContentGroup collection,
        Paginator paginator,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(paginator);
        ArgumentNullException.ThrowIfNull(allTaxonomies);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var layoutPath = ResolveLayoutPath(themePath,
            $"{collection.Name}-index.html",
            "index.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
        {
            var indexUrl = paginator.Page == 1
                ? collection.IndexUrl.OriginalString
                : $"{collection.IndexUrl.OriginalString.TrimEnd('/')}/page/{paginator.Page}/";
            var so = BuildCommonScriptObject(site, allTaxonomies, themePath, ctx, indexUrl, plugins, collection.Plugins);
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
        string themePath,
        IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(paginator);
        ArgumentNullException.ThrowIfNull(allTaxonomies);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var layoutPath = ResolveLayoutPath(themePath,
            $"taxonomy-{term.Taxonomy.Name}.html",
            "taxonomy.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
        {
            var termPageUrl = paginator.Page == 1
                ? term.Url.OriginalString
                : $"{term.Url.OriginalString.TrimEnd('/')}/page/{paginator.Page}/";
            var so = BuildCommonScriptObject(site, allTaxonomies, themePath, ctx, termPageUrl, plugins, null);
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
        string themePath,
        IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(taxonomy);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(allTaxonomies);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var overviewUrl = GetTaxonomyOverviewUrl(taxonomy);

        var layoutPath = ResolveLayoutPath(themePath,
            $"taxonomy-{taxonomy.Name}-index.html",
            "taxonomy-index.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
        {
            var so = BuildCommonScriptObject(site, allTaxonomies, themePath, ctx, overviewUrl.OriginalString, plugins, null);
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
        TemplateContext context,
        IReadOnlyList<PluginDefinition> plugins)
    {
        var so = BuildCommonScriptObject(site, allTaxonomies, themePath, context, item.Url.OriginalString, plugins, item.Collection.Plugins);

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
        pageObj.Add("collection", new { name = item.Collection.Name, url = item.Collection.IndexUrl.OriginalString, feed = item.Collection.Feed, plugins = item.Collection.Plugins });
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
        TemplateContext context,
        string? currentUrl,
        IReadOnlyList<PluginDefinition> plugins,
        Dictionary<string, object>? currentCollectionPlugins)
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
            kvp => BuildCollectionObject(kvp.Value));
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

        // menus — active flag computed from currentUrl
        var menusObj = new ScriptObject();
        foreach (var (name, menu) in site.Menus)
            menusObj.Add(name, menu.Items.Select(i => BuildMenuItemObject(i, currentUrl)).ToList());
        so.Add("menus", menusObj);

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

        // plugin_asset_url
        so.Import("plugin_asset_url", new Func<string, string, string>(
            (pluginName, path) => $"{assetPrefix}/plugins/{pluginName}/{path.TrimStart('/')}"));

        // slot — renders all plugin partials for the given slot name
        so.Import("slot", new Func<string, string>(slotName =>
        {
            var applicablePlugins = plugins
                .Where(p => p.Slots.Contains(slotName, StringComparer.OrdinalIgnoreCase))
                .Where(p => IsPluginEnabledForCollection(currentCollectionPlugins, Path.GetFileName(p.Directory)))
                .OrderBy(p => GetPluginPriority(site, Path.GetFileName(p.Directory)));

            var sb = new System.Text.StringBuilder();
            foreach (var plugin in applicablePlugins)
            {
                var pluginKey = Path.GetFileName(plugin.Directory);
                // Lookup: theme-override → plugin-default
                var themeOverridePath = Path.Combine(themePath, "plugins", pluginKey, "slots", $"{slotName}.html");
                var pluginDefaultPath = Path.Combine(plugin.Directory, "slots", $"{slotName}.html");

                string? slotFilePath;
                if (File.Exists(themeOverridePath))
                    slotFilePath = themeOverridePath;
                else if (File.Exists(pluginDefaultPath))
                    slotFilePath = pluginDefaultPath;
                else
                    slotFilePath = null;

                if (slotFilePath is null) continue;

                var slotTemplate = Template.Parse(File.ReadAllText(slotFilePath), slotFilePath);
                sb.Append(slotTemplate.Render(context));
            }
            return sb.ToString();
        }));

        return so;
    }

    private static bool IsPluginEnabledForCollection(Dictionary<string, object>? collectionPlugins, string pluginKey)
    {
        if (collectionPlugins is null || !collectionPlugins.TryGetValue(pluginKey, out var raw))
            return false;

        var enabledVal = raw switch
        {
            IDictionary<object, object> yamlDict when yamlDict.TryGetValue("enabled", out var v) => v,
            IDictionary<string, object> strDict when strDict.TryGetValue("enabled", out var v) => v,
            _ => null
        };

        if (enabledVal is null) return false;
        if (enabledVal is bool b) return b;
        return string.Equals(enabledVal.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPluginPriority(SiteConfiguration site, string pluginKey)
    {
        if (!site.Plugins.TryGetValue(pluginKey, out var raw)) return int.MaxValue;

        var priorityVal = raw switch
        {
            IDictionary<object, object> yamlDict when yamlDict.TryGetValue("priority", out var v) => v,
            IDictionary<string, object> strDict when strDict.TryGetValue("priority", out var v) => v,
            _ => null
        };

        if (priorityVal is int i) return i;
        return int.TryParse(priorityVal?.ToString(), out var p) ? p : int.MaxValue;
    }

    private static object BuildCollectionObject(ContentGroup collection)
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

    private static object BuildMenuItemObject(MenuItem item, string? currentUrl)
    {
        var children = item.Children.Select(c => BuildMenuItemObject(c, currentUrl)).ToList();
        var active = IsMenuItemActive(item, currentUrl);
        return new
        {
            title = item.Title,
            url = item.Url?.OriginalString,
            external = item.External,
            active,
            children
        };
    }

    private static bool IsMenuItemActive(MenuItem item, string? currentUrl)
    {
        if (currentUrl is null) return false;
        var selfActive = item.Url is not null &&
            string.Equals(item.Url.OriginalString, currentUrl, StringComparison.OrdinalIgnoreCase);
        if (selfActive) return true;
        foreach (var child in item.Children)
        {
            if (IsMenuItemActive(child, currentUrl)) return true;
        }
        return false;
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

