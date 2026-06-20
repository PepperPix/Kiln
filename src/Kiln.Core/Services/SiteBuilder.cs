namespace Kiln.Services;

using System.Collections.ObjectModel;
using System.Diagnostics;
using Kiln.Models;

public sealed class SiteBuilder(
    IContentReader contentReader,
    ITemplateRenderer templateRenderer,
    IPermalinkGenerator permalinkGenerator,
    ISiteConfigLoader configLoader,
    IPluginLoader pluginLoader) : ISiteBuilder
{
    public async Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new Collection<string>();
        var errors = new Collection<string>();

        // Load configuration
        var config = configLoader.Load(projectPath);
        var outputDir = Path.Combine(projectPath, config.OutputDir);
        var themePath = Path.Combine(projectPath, config.ThemesDir, config.Theme);

        if (!Directory.Exists(themePath))
        {
            errors.Add($"Theme directory not found: {themePath}");
            return MakeResult(0, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);
        }

        // Discover plugins
        var plugins = pluginLoader.LoadPlugins(projectPath);

        // Read all collections and assign URLs
        var allItems = new List<ContentItem>();
        foreach (var collection in config.Collections.Values)
        {
            var items = contentReader.ReadCollection(collection, projectPath);
            foreach (var item in items)
            {
                item.Url = permalinkGenerator.Generate(item, collection);
                item.OutputPath = ToOutputPath(item.Url);
                collection.Items.Add(item);
            }

            allItems.AddRange(items);
        }

        if (config.Home?.Collection is { } homeCollectionName)
        {
            var promoted = config.Collections[homeCollectionName];
            promoted.IndexUrlOverride = new Uri("/", UriKind.Relative);
            if (promoted.Paginate is null)
                errors.Add($"home.collection requires 'paginate' on collection '{homeCollectionName}'.");
        }

        if (config.Home?.Page is { } homePageRel)
        {
            var homePageAbsolute = Path.Combine(projectPath, homePageRel);
            if (!File.Exists(homePageAbsolute))
            {
                errors.Add($"home.page not found: {homePageRel}");
            }
            else
            {
                try
                {
                    var homeCollection = new ContentGroup { Name = "home", Layout = "home" };
                    var homeItem = contentReader.ReadSingleFile(homePageAbsolute, homeCollection);
                    homeItem.Url = new Uri("/", UriKind.Relative);
                    homeItem.OutputPath = ToOutputPath(homeItem.Url);
                    homeCollection.Items.Add(homeItem);
                    allItems.Add(homeItem);
                }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    errors.Add($"home.page could not be read: {homePageRel} ({ex.Message})");
                }
            }
        }

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        // Set next/prev navigation within each collection
        foreach (var collection in config.Collections.Values)
        {
            var published = collection.Items
                .Where(i => !i.Draft || includeDrafts)
                .ToList();
            for (var i = 0; i < published.Count; i++)
            {
                published[i].Prev = i > 0 ? published[i - 1] : null;
                published[i].Next = i < published.Count - 1 ? published[i + 1] : null;
            }
        }

        // Resolve cross-collection references (e.g. author: marcel → authors item)
        foreach (var (collName, collection) in config.Collections)
        {
            foreach (var (frontmatterKey, targetCollName) in collection.References)
            {
                if (!config.Collections.TryGetValue(targetCollName, out var targetCollection))
                {
                    warnings.Add($"Collection '{collName}': reference field '{frontmatterKey}' targets unknown collection '{targetCollName}'");
                    continue;
                }

                foreach (var item in collection.Items)
                {
                    if (!item.Extra.TryGetValue(frontmatterKey, out var rawValue) || rawValue is not string slugValue)
                        continue;

                    var refItem = targetCollection.Items.FirstOrDefault(
                        i => string.Equals(i.Slug, slugValue, StringComparison.OrdinalIgnoreCase));

                    if (refItem is null)
                        warnings.Add($"'{item.RelativePath}': reference '{frontmatterKey}: {slugValue}' not found in collection '{targetCollName}'");
                    else
                        item.ResolvedReferences[frontmatterKey] = refItem;
                }
            }
        }

        // Extract taxonomy terms (aggregate across all collections)
        var allTaxonomyTerms = ExtractTaxonomyTerms(config, includeDrafts);
        var sharedRenderContext = SharedRenderContext.Build(config, allTaxonomyTerms);

        // Collect all virtual page URLs for collision checking
        var virtualUrls = CollectVirtualUrls(config, allTaxonomyTerms, includeDrafts);

        // Permalink collision check: content items + virtual pages
        var urlToSources = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in allItems)
        {
            var url = item.Url.OriginalString;
            if (!urlToSources.TryGetValue(url, out var sources))
                urlToSources[url] = sources = [];
            sources.Add(item.RelativePath);
        }
        foreach (var virtualUrl in virtualUrls)
        {
            if (!urlToSources.TryGetValue(virtualUrl, out var sources))
                urlToSources[virtualUrl] = sources = [];
            sources.Add($"<virtual>");
        }
        foreach (var (url, sources) in urlToSources.Where(kvp => kvp.Value.Count > 1))
        {
            var sourcesText = string.Join(", ", sources);
            errors.Add($"Permalink collision — '{url}' is generated by: {sourcesText}");
        }

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        // Resolve menu references
        ResolveMenuRefs(config, allItems, virtualUrls, warnings, errors);

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        // Clean and create output directory
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
        Directory.CreateDirectory(outputDir);

        var rendered = 0;
        var skippedDrafts = 0;

        // Render content items
        foreach (var item in allItems)
        {
            ct.ThrowIfCancellationRequested();

            if (item.Draft && !includeDrafts)
            {
                skippedDrafts++;
                continue;
            }

            try
            {
                var html = templateRenderer.Render(item, sharedRenderContext, config, themePath, plugins);
                var outputPath = Path.Combine(outputDir, item.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllTextAsync(outputPath, html, ct).ConfigureAwait(false);
                rendered++;
            }
#pragma warning disable CA1031 // Intentional: one file error should not abort the entire build
            catch (Exception ex)
#pragma warning restore CA1031
            {
                errors.Add($"Error rendering '{item.RelativePath}': {ex.Message}");
            }
        }

        // Render collection index pages (for collections with Paginate > 0)
        foreach (var collection in config.Collections.Values)
        {
            if (!(collection.Paginate > 0)) continue;

            var nonDraftItems = collection.Items
                .Where(i => !i.Draft || includeDrafts)
                .ToList();
            if (nonDraftItems.Count == 0) continue;

            var paginators = BuildPaginators(nonDraftItems, collection.Paginate!.Value, collection.IndexUrl.OriginalString);
            foreach (var paginator in paginators)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var html = templateRenderer.RenderCollectionIndex(collection, paginator, sharedRenderContext, config, themePath, plugins);
                    var indexBase = collection.IndexUrl.OriginalString;
                    var pageUrl = paginator.Page == 1
                        ? indexBase
                        : $"{indexBase.TrimEnd('/')}/page/{paginator.Page}/";
                    var outputPath = Path.Combine(outputDir, ToOutputPath(new Uri(pageUrl, UriKind.Relative)));
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                    await File.WriteAllTextAsync(outputPath, html, ct).ConfigureAwait(false);
                    rendered++;
                }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    errors.Add($"Error rendering collection index '{collection.Name}': {ex.Message}");
                }
            }
        }

        // Render taxonomy term and overview pages
        foreach (var (taxName, terms) in allTaxonomyTerms)
        {
            if (!config.Taxonomies.TryGetValue(taxName, out var taxDef)) continue;

            // Taxonomy overview page
            ct.ThrowIfCancellationRequested();
            try
            {
                var overviewUrl = TemplateRenderer.GetTaxonomyOverviewUrl(taxDef);
                var html = templateRenderer.RenderTaxonomyOverview(taxDef, terms, sharedRenderContext, config, themePath, plugins);
                var outputPath = Path.Combine(outputDir, ToOutputPath(overviewUrl));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllTextAsync(outputPath, html, ct).ConfigureAwait(false);
                rendered++;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                errors.Add($"Error rendering taxonomy overview '{taxName}': {ex.Message}");
            }

            // Taxonomy term pages
            foreach (var term in terms)
            {
                ct.ThrowIfCancellationRequested();
                var paginators = taxDef.Paginate > 0
                    ? BuildPaginators(term.Items.ToList(), taxDef.Paginate!.Value, term.Url.OriginalString)
                    : [new Paginator { Items = term.Items, Page = 1, TotalPages = 1, TotalItems = term.Count }];

                foreach (var paginator in paginators)
                {
                    try
                    {
                        var html = templateRenderer.RenderTaxonomyTerm(term, paginator, sharedRenderContext, config, themePath, plugins);
                        var pageUrl = paginator.Page == 1
                            ? term.Url.OriginalString
                            : $"{term.Url.OriginalString.TrimEnd('/')}/page/{paginator.Page}/";
                        var outputPath = Path.Combine(outputDir, ToOutputPath(new Uri(pageUrl, UriKind.Relative)));
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                        await File.WriteAllTextAsync(outputPath, html, ct).ConfigureAwait(false);
                        rendered++;
                    }
#pragma warning disable CA1031
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        errors.Add($"Error rendering taxonomy term '{taxName}/{term.Slug}': {ex.Message}");
                    }
                }
            }
        }

        // Emit a 404 page only when the theme provides a dedicated '404.html' layout.
        // We deliberately do NOT fall back to 'default.html' here: that layout expects a
        // 'page' content item (e.g. page.content), which the not-found page does not bind.
        // A theme without a 404 layout simply gets no 404 page rather than a failed build.
        var hasNotFoundLayout = File.Exists(Path.Combine(themePath, "layouts", "404.html"));
        if (hasNotFoundLayout)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var notFoundHtml = templateRenderer.RenderNotFound(sharedRenderContext, config, themePath, plugins);
                await File.WriteAllTextAsync(Path.Combine(outputDir, "404.html"), notFoundHtml, ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                errors.Add($"Error rendering not-found page: {ex.Message}");
            }
        }

        // Copy static assets from theme → _site/assets/ (lowest priority)
        var assetsOutputDir = Path.Combine(outputDir, "assets");
        var themeStaticDir = Path.Combine(themePath, "static");
        if (Directory.Exists(themeStaticDir))
            CopyDirectory(themeStaticDir, assetsOutputDir);

        // Copy static assets from site → _site/assets/ (overrides theme, warns on collision)
        var siteStaticDir = Path.Combine(projectPath, "static");
        if (Directory.Exists(siteStaticDir))
            CopyDirectoryWithCollisionWarning(siteStaticDir, assetsOutputDir, config.Theme, warnings);

        // Copy co-located assets from Page Bundles → _site/assets/content/<collection>/<slug>/
        foreach (var item in allItems.Where(static i => i.AssetDirectory is not null))
        {
            var destDir = Path.Combine(assetsOutputDir, "content", item.Collection.Name, item.Slug);
            CopyNonMarkdownFiles(item.AssetDirectory!, destDir);
        }

        // Copy plugin assets: plugins/<name>/static/ → _site/assets/plugins/<name>/
        foreach (var plugin in plugins)
        {
            var pluginStaticDir = Path.Combine(plugin.Directory, "static");
            if (!Directory.Exists(pluginStaticDir)) continue;
            var pluginKey = Path.GetFileName(plugin.Directory);
            var pluginAssetsDir = Path.Combine(assetsOutputDir, "plugins", pluginKey);
            CopyDirectory(pluginStaticDir, pluginAssetsDir);
        }

        // Generate sitemap.xml
        var sitemapContent = SitemapGenerator.Generate(config, allItems, allTaxonomyTerms, includeDrafts);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "sitemap.xml"), sitemapContent, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);

        // Generate Atom feeds for collections with feed: true
        foreach (var collection in config.Collections.Values)
        {
            if (!collection.Feed) continue;
            var feedContent = FeedGenerator.GenerateAtom(collection, collection.Items, config);
            var indexRelPath = collection.IndexUrl.OriginalString.Trim('/');
            var feedDir = string.IsNullOrEmpty(indexRelPath)
                ? outputDir
                : Path.Combine(outputDir, indexRelPath);
            Directory.CreateDirectory(feedDir);
            await File.WriteAllTextAsync(Path.Combine(feedDir, "feed.xml"), feedContent, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);
        }

        // Generate robots.txt
        var robotsTxt = $"User-agent: *\nAllow: /\n\nSitemap: {config.BaseUrl.ToString().TrimEnd('/')}/sitemap.xml\n";
        await File.WriteAllTextAsync(Path.Combine(outputDir, "robots.txt"), robotsTxt, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);

        stopwatch.Stop();
        return MakeResult(allItems.Count, rendered, skippedDrafts, stopwatch.Elapsed, outputDir, warnings, errors);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static void ResolveMenuRefs(
        SiteConfiguration config,
        List<ContentItem> allItems,
        List<string> virtualUrls,
        Collection<string> warnings,
        Collection<string> errors)
    {
        if (config.Menus.Count == 0) return;

        var knownUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in allItems)
            knownUrls.Add(item.Url.OriginalString);
        foreach (var url in virtualUrls)
            knownUrls.Add(url);

        foreach (var menu in config.Menus.Values)
        {
            foreach (var item in menu.Items)
                ResolveMenuItemRef(item, menu.Name, config, knownUrls, warnings, errors);
        }
    }

    private static void ResolveMenuItemRef(
        MenuItem item,
        string menuName,
        SiteConfiguration config,
        HashSet<string> knownUrls,
        Collection<string> warnings,
        Collection<string> errors)
    {
        if (item.Ref is not null)
        {
            var resolved = ResolveRef(item.Ref, config, menuName, item.Title, errors);
            if (resolved is not null)
                item.Url = resolved;
        }
        else if (item.Url is not null && !item.External)
        {
            if (!knownUrls.Contains(item.Url.OriginalString))
                warnings.Add($"Menu '{menuName}': URL '{item.Url.OriginalString}' ('{item.Title}') does not match any known page");
        }

        foreach (var child in item.Children)
            ResolveMenuItemRef(child, menuName, config, knownUrls, warnings, errors);
    }

    private static Uri? ResolveRef(
        string refValue,
        SiteConfiguration config,
        string menuName,
        string itemTitle,
        Collection<string> errors)
    {
        // ref: posts/ → collection index URL
        if (refValue.EndsWith('/'))
        {
            var collectionName = refValue.TrimEnd('/');
            if (!config.Collections.TryGetValue(collectionName, out var collection))
            {
                errors.Add($"Menu '{menuName}': ref '{refValue}' ('{itemTitle}') targets unknown collection '{collectionName}'");
                return null;
            }
            return collection.IndexUrl;
        }

        // ref: pages/about → item URL in collection
        var slashIdx = refValue.IndexOf('/', StringComparison.OrdinalIgnoreCase);
        if (slashIdx < 0)
        {
            errors.Add($"Menu '{menuName}': ref '{refValue}' ('{itemTitle}') is invalid — use 'collection/slug' or 'collection/'");
            return null;
        }

        var refCollectionName = refValue[..slashIdx];
        var refSlug = refValue[(slashIdx + 1)..];

        if (!config.Collections.TryGetValue(refCollectionName, out var refCollection))
        {
            errors.Add($"Menu '{menuName}': ref '{refValue}' ('{itemTitle}') targets unknown collection '{refCollectionName}'");
            return null;
        }

        var found = refCollection.Items.FirstOrDefault(
            i => string.Equals(i.Slug, refSlug, StringComparison.OrdinalIgnoreCase));

        if (found is null)
        {
            errors.Add($"Menu '{menuName}': ref '{refValue}' ('{itemTitle}') — item '{refSlug}' not found in collection '{refCollectionName}'");
            return null;
        }

        return found.Url;
    }

    private static Dictionary<string, IReadOnlyList<TaxonomyTerm>> ExtractTaxonomyTerms(
        SiteConfiguration config, bool includeDrafts)
    {
        var result = new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (taxName, taxDef) in config.Taxonomies)
        {
            var termsBySlug = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);

            foreach (var collection in config.Collections.Values)
            {
                var collectionUsesTaxonomy = false;
                foreach (var t in collection.Taxonomies)
                {
                    if (string.Equals(t, taxName, StringComparison.OrdinalIgnoreCase))
                    {
                        collectionUsesTaxonomy = true;
                        break;
                    }
                }
                if (!collectionUsesTaxonomy) continue;

                foreach (var item in collection.Items)
                {
                    if (item.Draft && !includeDrafts) continue;
                    if (!item.Taxonomies.TryGetValue(taxName, out var taxValue)) continue;

                    IEnumerable<string> termValues;
                    if (taxValue is IEnumerable<string> enumerable)
                        termValues = enumerable;
                    else if (taxValue is string single)
                        termValues = [single];
                    else
                        termValues = [];

                    foreach (var termName in termValues)
                    {
                        if (string.IsNullOrWhiteSpace(termName)) continue;

                        var slug = TemplateRenderer.ToSlug(termName);
                        if (!termsBySlug.TryGetValue(slug, out var term))
                        {
                            var termUrl = taxDef.Permalink.Replace(":slug", slug, StringComparison.OrdinalIgnoreCase);
                            term = new TaxonomyTerm
                            {
                                Name = termName,
                                Slug = slug,
                                Taxonomy = taxDef,
                                Url = new Uri(termUrl, UriKind.Relative)
                            };
                            termsBySlug[slug] = term;
                        }
                        term.Items.Add(item);
                    }
                }
            }

            result[taxName] = [.. termsBySlug.Values.OrderByDescending(t => t.Count)];
        }

        return result;
    }

    private static List<string> CollectVirtualUrls(
        SiteConfiguration config,
        Dictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomyTerms,
        bool includeDrafts)
    {
        var urls = new List<string>();

        // Collection index pages
        foreach (var collection in config.Collections.Values)
        {
            if (!(collection.Paginate > 0)) continue;
            var nonDraftCount = collection.Items.Count(i => !i.Draft || includeDrafts);
            if (nonDraftCount == 0) continue;

            var totalPages = (int)Math.Ceiling(nonDraftCount / (double)collection.Paginate!.Value);
            var indexUrl = collection.IndexUrl;
            urls.Add(indexUrl.OriginalString);
            for (var p = 2; p <= totalPages; p++)
                urls.Add($"{indexUrl.OriginalString.TrimEnd('/')}/page/{p}/");
        }

        // Taxonomy overview and term pages
        foreach (var (taxName, terms) in allTaxonomyTerms)
        {
            if (!config.Taxonomies.TryGetValue(taxName, out var taxDef)) continue;

            urls.Add(TemplateRenderer.GetTaxonomyOverviewUrl(taxDef).OriginalString);

            foreach (var term in terms)
            {
                urls.Add(term.Url.OriginalString);
                if (taxDef.Paginate > 0)
                {
                    var totalPages = (int)Math.Ceiling(term.Count / (double)taxDef.Paginate!.Value);
                    for (var p = 2; p <= totalPages; p++)
                        urls.Add($"{term.Url.OriginalString.TrimEnd('/')}/page/{p}/");
                }
            }
        }

        return urls;
    }

#pragma warning disable CA1859 // IReadOnlyList intentional: supports both List and Collection callers
    private static List<Paginator> BuildPaginators(
        IReadOnlyList<ContentItem> items, int pageSize, string baseUrl)
#pragma warning restore CA1859
    {
        var totalPages = (int)Math.Ceiling(items.Count / (double)pageSize);
        if (totalPages == 0) totalPages = 1;

        var paginators = new List<Paginator>(totalPages);
        var baseNormalized = baseUrl.TrimEnd('/') + "/";
        const int firstPage = 1;
        const int secondPage = 2;
        for (var page = firstPage; page <= totalPages; page++)
        {
            var pageItems = items.Skip((page - firstPage) * pageSize).Take(pageSize).ToList();
            Uri? nextUrl = page < totalPages
                ? new Uri($"{baseNormalized}page/{page + firstPage}/", UriKind.Relative)
                : null;
            Uri? prevUrl;
            if (page == firstPage)
                prevUrl = null;
            else if (page == secondPage)
                prevUrl = new Uri(baseNormalized, UriKind.Relative);
            else
                prevUrl = new Uri($"{baseNormalized}page/{page - firstPage}/", UriKind.Relative);

            paginators.Add(new Paginator
            {
                Items = pageItems,
                Page = page,
                TotalPages = totalPages,
                TotalItems = items.Count,
                NextUrl = nextUrl,
                PrevUrl = prevUrl
            });
        }
        return paginators;
    }

    private static string ToOutputPath(Uri url)
    {
        // /blog/hello-world/ → blog/hello-world/index.html
        var normalized = url.OriginalString.Trim('/');
        return string.IsNullOrEmpty(normalized)
            ? "index.html"
            : Path.Combine(normalized, "index.html");
    }

    private static BuildResult MakeResult(int total, int rendered, int skipped, TimeSpan duration, string outputDir, Collection<string> warnings, Collection<string> errors)
    {
        return new BuildResult
        {
            TotalFiles = total,
            RenderedFiles = rendered,
            SkippedDrafts = skipped,
            Duration = duration,
            OutputDirectory = outputDir,
            Warnings = warnings,
            Errors = errors
        };
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
        }
    }

    private static void CopyDirectoryWithCollisionWarning(
        string sourceDir, string destDir, string themeName, Collection<string> warnings)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);

            if (File.Exists(destPath))
                warnings.Add($"Asset '{relativePath}' in static/ overrides same file from theme '{themeName}'");

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
        }
    }

    private static void CopyNonMarkdownFiles(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetExtension(file).Equals(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(destDir);
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }
    }
}

