namespace Kiln.Services;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Kiln.Abstractions;
using Kiln.Models;

#pragma warning disable S107 // 8 DI-injected services; no sensible split without a facade.
public sealed class SiteBuilder(
    IContentReader contentReader,
    ITemplateRenderer templateRenderer,
    IPermalinkGenerator permalinkGenerator,
    ISiteConfigLoader configLoader,
    IPluginLoader pluginLoader,
    IEnumerable<IAssetMinifier> assetMinifiers,
    IImageOptimizer imageOptimizer,
    IAssetReferenceIndexBuilder assetReferenceIndexBuilder) : ISiteBuilder
#pragma warning restore S107
{
    private readonly IReadOnlyList<IAssetMinifier> _assetMinifiers = [.. assetMinifiers];

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default)
        => BuildAsync(projectPath, includeDrafts, BuildEnvironment.Development, progress: null, ct);

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, CancellationToken ct)
        => BuildAsync(projectPath, includeDrafts, environment, progress: null, ct);

    public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, IProgress<BuildProgress>? progress, CancellationToken ct)
        => BuildAsync(projectPath, includeDrafts, environment, progress, baseUrlOverride: null, ct);

    public async Task<BuildResult> BuildAsync(
        string projectPath,
        bool includeDrafts,
        BuildEnvironment environment,
        IProgress<BuildProgress>? progress,
        Uri? baseUrlOverride,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new Collection<string>();
        var errors = new Collection<string>();

        if (!TryLoadConfiguration(projectPath, out var config, out var outputDir, out var themePath, errors))
            return MakeResult(0, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        if (baseUrlOverride is not null)
            config = config.WithBaseUrl(baseUrlOverride);

        // Discover plugins
        var plugins = pluginLoader.LoadPlugins(projectPath);

        // Read all collections and assign URLs
        var allItems = ReadAllContent(config, projectPath, plugins, warnings, errors);

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        // Set next/prev navigation within each collection
        ComputeNextPrevLinks(config, includeDrafts);

        // Resolve cross-collection references (e.g. author: marcel → authors item)
        ResolveCrossCollectionReferences(config, warnings);

        // Extract taxonomy terms (aggregate across all collections)
        var allTaxonomyTerms = ExtractTaxonomyTerms(config, includeDrafts);

        // Build navigation tree once per build
        var publishedItems = allItems.Where(i => !i.Draft || includeDrafts).ToList();
        var navTree = NavigationTreeBuilder.Build(publishedItems, config.BasePath);
        var sharedRenderContext = SharedRenderContext.Build(config, allTaxonomyTerms, navTree);

        // Collect all virtual page URLs for collision checking
        var virtualUrls = CollectVirtualUrls(config, allTaxonomyTerms, includeDrafts);

        // Permalink collision check: content items + virtual pages
        CheckPermalinkCollisions(allItems, virtualUrls, errors);

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        // Resolve menu references
        ResolveMenuRefs(config, allItems, virtualUrls, warnings, errors);

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        var useWriteThenPrune = environment == BuildEnvironment.Development;
        var generatedFiles = useWriteThenPrune
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : null;

        // For development builds, write-then-prune avoids transient 404 windows during serve.
        if (!useWriteThenPrune && Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
        Directory.CreateDirectory(outputDir);

        var render = new RenderPassContext(sharedRenderContext, config, themePath, plugins, outputDir, generatedFiles);

        // Render content items
        var (rendered, skippedDrafts) = await RenderContentItemsAsync(
            allItems, includeDrafts, render, progress, errors, ct).ConfigureAwait(false);

        // Render collection index pages (for collections with Paginate > 0)
        rendered += await RenderCollectionIndexesAsync(includeDrafts, render, errors, ct).ConfigureAwait(false);

        // Render taxonomy term and overview pages
        rendered += await RenderTaxonomyPagesAsync(allTaxonomyTerms, render, errors, ct).ConfigureAwait(false);

        // Emit a 404 page only when the theme provides a dedicated '404.html' layout.
        await RenderNotFoundPageAsync(render, errors, ct).ConfigureAwait(false);

        // Copy assets (theme → site → page bundles → plugins), then rewrite renamed image refs
        RunAssetCopyPipeline(allItems, projectPath, environment, warnings, render);

        // Generate sitemap.xml, Atom feeds, robots.txt
        await WriteFeedsAndMetaAsync(config, allItems, allTaxonomyTerms, includeDrafts, outputDir, generatedFiles, ct).ConfigureAwait(false);

        // Production asset pipeline: minify → fingerprint → link-check
        var tally = new BuildTally(allItems.Count, rendered, skippedDrafts, stopwatch.Elapsed);
        var earlyResult = await RunProductionAssetPipelineAsync(config, environment, outputDir, tally, warnings, errors, ct).ConfigureAwait(false);
        if (earlyResult is not null)
            return earlyResult;

        if (generatedFiles is not null)
            PruneStaleOutputs(outputDir, generatedFiles);

        stopwatch.Stop();
        return MakeResult(allItems.Count, rendered, skippedDrafts, stopwatch.Elapsed, outputDir, warnings, errors);
    }

    // ── Build phases ─────────────────────────────────────────────────────

    private bool TryLoadConfiguration(
        string projectPath,
        out SiteConfiguration config,
        out string outputDir,
        out string themePath,
        Collection<string> errors)
    {
        config = configLoader.Load(projectPath);
        outputDir = Path.Combine(projectPath, config.OutputDir);
        themePath = Path.Combine(projectPath, config.ThemesDir, config.Theme);

        if (!Directory.Exists(themePath))
        {
            errors.Add($"Theme directory not found: {themePath}");
            return false;
        }

        return true;
    }

    private List<ContentItem> ReadAllContent(
        SiteConfiguration config,
        string projectPath,
        IReadOnlyList<PluginDefinition> plugins,
        Collection<string> warnings,
        Collection<string> errors)
    {
        var allItems = new List<ContentItem>();
        foreach (var collection in config.Collections.Values)
        {
            var items = contentReader.ReadCollection(collection, projectPath, plugins, warnings);
            foreach (var item in items)
            {
                item.Url = permalinkGenerator.Generate(item, collection, config.BasePath);
                item.OutputPath = ToOutputPath(item.Url, config.BasePath);
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
                    var homeItem = contentReader.ReadSingleFile(homePageAbsolute, homeCollection, plugins, warnings);
                    homeItem.Url = new Uri(SiteConfiguration.ApplyBasePath(config.BasePath, new Uri("/", UriKind.Relative)), UriKind.Relative);
                    homeItem.OutputPath = ToOutputPath(homeItem.Url, config.BasePath);
                    homeCollection.Items.Add(homeItem);
                    allItems.Add(homeItem);
                }
#pragma warning disable CA1031 // Intentional: an unreadable home page should not abort the entire build
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    errors.Add($"home.page could not be read: {homePageRel} ({ex.Message})");
                }
            }
        }

        return allItems;
    }

    private static void ComputeNextPrevLinks(SiteConfiguration config, bool includeDrafts)
    {
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
    }

    private static void ResolveCrossCollectionReferences(SiteConfiguration config, Collection<string> warnings)
    {
        var slugIndexCache = new Dictionary<ContentGroup, Dictionary<string, ContentItem>>();

        foreach (var (collName, collection) in config.Collections)
        {
            foreach (var (frontmatterKey, targetCollName) in collection.References)
            {
                if (!config.Collections.TryGetValue(targetCollName, out var targetCollection))
                {
                    warnings.Add($"Collection '{collName}': reference field '{frontmatterKey}' targets unknown collection '{targetCollName}'");
                    continue;
                }

                if (!slugIndexCache.TryGetValue(targetCollection, out var slugIndex))
                {
                    slugIndex = BuildSlugIndex(targetCollection);
                    slugIndexCache[targetCollection] = slugIndex;
                }

                foreach (var item in collection.Items)
                {
                    if (!item.Extra.TryGetValue(frontmatterKey, out var rawValue) || rawValue is not string slugValue)
                        continue;

                    var refItem = slugIndex.GetValueOrDefault(slugValue);

                    if (refItem is null)
                        warnings.Add($"'{item.RelativePath}': reference '{frontmatterKey}: {slugValue}' not found in collection '{targetCollName}'");
                    else
                        item.ResolvedReferences[frontmatterKey] = refItem;
                }
            }
        }
    }

    private static void CheckPermalinkCollisions(List<ContentItem> allItems, List<string> virtualUrls, Collection<string> errors)
    {
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
    }

    /// <summary>
    /// Bundles the render-time state shared by all page-rendering phases (content items,
    /// collection indexes, taxonomy pages, 404, asset copying) — keeps phase method parameter
    /// counts within the S107 limit without changing behavior.
    /// </summary>
    private sealed record RenderPassContext(
        SharedRenderContext SharedContext,
        SiteConfiguration Config,
        string ThemePath,
        IReadOnlyList<PluginDefinition> Plugins,
        string OutputDir,
        HashSet<string>? GeneratedFiles);

    private async Task<(int Rendered, int SkippedDrafts)> RenderContentItemsAsync(
        List<ContentItem> allItems,
        bool includeDrafts,
        RenderPassContext render,
        IProgress<BuildProgress>? progress,
        Collection<string> errors,
        CancellationToken ct)
    {
        var rendered = 0;
        var skippedDrafts = 0;

        foreach (var item in allItems)
        {
            ct.ThrowIfCancellationRequested();

            if (item.Draft && !includeDrafts)
            {
                skippedDrafts++;
                progress?.Report(new BuildProgress("Rendering pages", rendered + skippedDrafts, allItems.Count));
                continue;
            }

            try
            {
                var html = templateRenderer.Render(item, render.SharedContext, render.Config, render.ThemePath, render.Plugins);
                var outputPath = Path.Combine(render.OutputDir, item.OutputPath);
                await WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
                rendered++;
            }
#pragma warning disable CA1031 // Intentional: one file error should not abort the entire build
            catch (Exception ex)
#pragma warning restore CA1031
            {
                errors.Add($"Error rendering '{item.RelativePath}': {ex.Message}");
            }

            progress?.Report(new BuildProgress("Rendering pages", rendered + skippedDrafts, allItems.Count));
        }

        return (rendered, skippedDrafts);
    }

    private async Task<int> RenderCollectionIndexesAsync(
        bool includeDrafts,
        RenderPassContext render,
        Collection<string> errors,
        CancellationToken ct)
    {
        var rendered = 0;

        foreach (var collection in render.Config.Collections.Values)
        {
            if (!(collection.Paginate > 0)) continue;

            var nonDraftItems = collection.Items
                .Where(i => !i.Draft || includeDrafts)
                .ToList();
            if (nonDraftItems.Count == 0) continue;

            var paginators = BuildPaginators(nonDraftItems, collection.Paginate!.Value, collection.IndexUrl.OriginalString, render.Config.BasePath);
            foreach (var paginator in paginators)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var html = templateRenderer.RenderCollectionIndex(collection, paginator, render.SharedContext, render.Config, render.ThemePath, render.Plugins);
                    var indexBase = collection.IndexUrl.OriginalString;
                    var pageUrl = paginator.Page == 1
                        ? indexBase
                        : $"{indexBase.TrimEnd('/')}/page/{paginator.Page}/";
                    var outputPath = Path.Combine(render.OutputDir, ToOutputPath(new Uri(pageUrl, UriKind.Relative), render.Config.BasePath));
                    await WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
                    rendered++;
                }
#pragma warning disable CA1031 // Intentional: one collection index page error should not abort the entire build
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    errors.Add($"Error rendering collection index '{collection.Name}': {ex.Message}");
                }
            }
        }

        return rendered;
    }

    private async Task<int> RenderTaxonomyPagesAsync(
        Dictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomyTerms,
        RenderPassContext render,
        Collection<string> errors,
        CancellationToken ct)
    {
        var rendered = 0;

        foreach (var (taxName, terms) in allTaxonomyTerms)
        {
            if (!render.Config.Taxonomies.TryGetValue(taxName, out var taxDef)) continue;

            // Taxonomy overview page
            ct.ThrowIfCancellationRequested();
            try
            {
                var overviewUrl = TemplateRenderer.GetTaxonomyOverviewUrl(taxDef);
                var html = templateRenderer.RenderTaxonomyOverview(taxDef, terms, render.SharedContext, render.Config, render.ThemePath, render.Plugins);
                var outputPath = Path.Combine(render.OutputDir, ToOutputPath(overviewUrl, render.Config.BasePath));
                await WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
                rendered++;
            }
#pragma warning disable CA1031 // Intentional: one taxonomy overview error should not abort the entire build
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
                    ? BuildPaginators(term.Items.ToList(), taxDef.Paginate!.Value, term.Url.OriginalString, render.Config.BasePath)
                    : [new Paginator { Items = term.Items, Page = 1, TotalPages = 1, TotalItems = term.Count }];

                foreach (var paginator in paginators)
                {
                    try
                    {
                        var html = templateRenderer.RenderTaxonomyTerm(term, paginator, render.SharedContext, render.Config, render.ThemePath, render.Plugins);
                        var pageUrl = paginator.Page == 1
                            ? term.Url.OriginalString
                            : $"{term.Url.OriginalString.TrimEnd('/')}/page/{paginator.Page}/";
                        var outputPath = Path.Combine(render.OutputDir, ToOutputPath(new Uri(pageUrl, UriKind.Relative), render.Config.BasePath));
                        await WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
                        rendered++;
                    }
#pragma warning disable CA1031 // Intentional: one taxonomy term page error should not abort the entire build
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        errors.Add($"Error rendering taxonomy term '{taxName}/{term.Slug}': {ex.Message}");
                    }
                }
            }
        }

        return rendered;
    }

    private async Task RenderNotFoundPageAsync(
        RenderPassContext render,
        Collection<string> errors,
        CancellationToken ct)
    {
        // Emit a 404 page only when the theme provides a dedicated '404.html' layout.
        // We deliberately do NOT fall back to 'default.html' here: that layout expects a
        // 'page' content item (e.g. page.content), which the not-found page does not bind.
        // A theme without a 404 layout simply gets no 404 page rather than a failed build.
        var hasNotFoundLayout = File.Exists(Path.Combine(render.ThemePath, "layouts", "404.html"));
        if (!hasNotFoundLayout) return;

        ct.ThrowIfCancellationRequested();
        try
        {
            var notFoundHtml = templateRenderer.RenderNotFound(render.SharedContext, render.Config, render.ThemePath, render.Plugins);
            var notFoundPath = Path.Combine(render.OutputDir, "404.html");
            await WriteOutputTextAsync(notFoundPath, notFoundHtml, render.GeneratedFiles, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Intentional: a 404 page rendering error should not abort the entire build
        catch (Exception ex)
#pragma warning restore CA1031
        {
            errors.Add($"Error rendering not-found page: {ex.Message}");
        }
    }

    private void RunAssetCopyPipeline(
        List<ContentItem> allItems,
        string projectPath,
        BuildEnvironment environment,
        Collection<string> warnings,
        RenderPassContext render)
    {
        var config = render.Config;
        var outputDir = render.OutputDir;
        var generatedFiles = render.GeneratedFiles;

        // Copy static assets from theme → _site/assets/ (lowest priority)
        var assetsOutputDir = Path.Combine(outputDir, "assets");
        var themeStaticDir = Path.Combine(render.ThemePath, "static");
        if (Directory.Exists(themeStaticDir))
            CopyDirectory(themeStaticDir, assetsOutputDir, generatedFiles);

        // Image optimization (Production only): only Site static/ and Page Bundle assets are
        // candidates, and only when actually referenced via <img src="/assets/..."> in already-
        // rendered HtmlContent. Theme/plugin static/ above and below are never optimized.
        var referencedImages = config.Images.Enabled && environment == BuildEnvironment.Production
            ? assetReferenceIndexBuilder.Build(allItems).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        var imageRenameManifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var imageCopyContext = new ImageCopyContext(outputDir, projectPath, config.Images, referencedImages, imageRenameManifest);

        // Copy static assets from site → _site/assets/ (overrides theme, warns on collision)
        var siteStaticDir = Path.Combine(projectPath, "static");
        if (Directory.Exists(siteStaticDir))
            CopyDirectoryWithCollisionWarning(siteStaticDir, assetsOutputDir, config.Theme, warnings, generatedFiles, imageCopyContext);

        // Copy co-located assets from Page Bundles → _site/assets/content/<collection>/<sectionPath>/<slug>/
        foreach (var item in allItems.Where(static i => i.AssetDirectory is not null))
        {
            var slugPath = string.IsNullOrEmpty(item.SectionPath) ? item.Slug : $"{item.SectionPath}/{item.Slug}";
            var destDir = Path.Combine(assetsOutputDir, "content", item.Collection.Name, slugPath);
            // Note: item.ImageOptimization == false already excluded this item's images from
            // 'referencedImages' above, so CopyOrOptimizeFile naturally skips optimizing them.
            CopyNonMarkdownFiles(item.AssetDirectory!, destDir, generatedFiles, imageCopyContext, warnings);
        }

        // Copy plugin assets: plugins/<name>/static/ → _site/assets/plugins/<name>/
        foreach (var plugin in render.Plugins)
        {
            var pluginStaticDir = Path.Combine(plugin.Directory, "static");
            if (!Directory.Exists(pluginStaticDir)) continue;
            var pluginKey = Path.GetFileName(plugin.Directory);
            var pluginAssetsDir = Path.Combine(assetsOutputDir, "plugins", pluginKey);
            CopyDirectory(pluginStaticDir, pluginAssetsDir, generatedFiles);
        }

        // Rewrite HTML/CSS references for any images renamed by optimization (e.g. WebP
        // conversion changing the extension) — must run before the fingerprint stage below,
        // so hashes cover the final on-disk bytes.
        if (imageRenameManifest.Count > 0)
            AssetPipeline.RewriteReferences(outputDir, imageRenameManifest);
    }

    private static async Task WriteFeedsAndMetaAsync(
        SiteConfiguration config,
        List<ContentItem> allItems,
        Dictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomyTerms,
        bool includeDrafts,
        string outputDir,
        HashSet<string>? generatedFiles,
        CancellationToken ct)
    {
        // Generate sitemap.xml
        var sitemapContent = SitemapGenerator.Generate(config, allItems, allTaxonomyTerms, includeDrafts);
        await WriteOutputTextAsync(Path.Combine(outputDir, "sitemap.xml"), sitemapContent, generatedFiles, ct, Encoding.UTF8).ConfigureAwait(false);

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
            await WriteOutputTextAsync(Path.Combine(feedDir, "feed.xml"), feedContent, generatedFiles, ct, Encoding.UTF8).ConfigureAwait(false);
        }

        // Generate robots.txt
        var robotsTxt = $"User-agent: *\nAllow: /\n\nSitemap: {config.BaseUrl.ToString().TrimEnd('/')}/sitemap.xml\n";
        await WriteOutputTextAsync(Path.Combine(outputDir, "robots.txt"), robotsTxt, generatedFiles, ct, Encoding.UTF8).ConfigureAwait(false);
    }

    /// <summary>
    /// Bundles the item/render counters needed by the final production asset pipeline phase
    /// (only used for the unknown-minifier early-return result) — keeps the phase method's
    /// parameter count within the S107 limit without changing behavior.
    /// </summary>
    private sealed record BuildTally(int TotalItems, int Rendered, int SkippedDrafts, TimeSpan Elapsed);

    private async Task<BuildResult?> RunProductionAssetPipelineAsync(
        SiteConfiguration config,
        BuildEnvironment environment,
        string outputDir,
        BuildTally tally,
        Collection<string> warnings,
        Collection<string> errors,
        CancellationToken ct)
    {
        if (environment != BuildEnvironment.Production)
            return null;

        var minifierId = config.Assets.Minifier;
        var selectedMinifier = _assetMinifiers.FirstOrDefault(m => string.Equals(m.Id, minifierId, StringComparison.OrdinalIgnoreCase));
        if (selectedMinifier is null)
        {
            var available = string.Join(", ", _assetMinifiers.Select(static m => $"'{m.Id}'"));
            errors.Add($"Unknown asset minifier id '{minifierId}'. Available: {available}");
            return MakeResult(tally.TotalItems, tally.Rendered, tally.SkippedDrafts, tally.Elapsed, outputDir, warnings, errors);
        }

        await AssetPipeline.RunAsync(outputDir, config.AssetPrefix, config.Build, selectedMinifier, warnings, errors, ct).ConfigureAwait(false);
        return null;
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
            // Menu URLs are authored site-relative (without the base path); known page URLs
            // already carry the base path prefix, so the same prefix must be applied before comparing.
            var resolvedUrl = SiteConfiguration.ApplyBasePath(config.BasePath, item.Url);
            if (!knownUrls.Contains(resolvedUrl))
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

    private static List<Paginator> BuildPaginators(
#pragma warning disable CA1859 // IReadOnlyList intentional: supports both List and Collection callers
        IReadOnlyList<ContentItem> items, int pageSize, string baseUrl, string basePath = "")
#pragma warning restore CA1859
    {
        var totalPages = (int)Math.Ceiling(items.Count / (double)pageSize);
        if (totalPages == 0) totalPages = 1;

        var paginators = new List<Paginator>(totalPages);
        const int firstPage = 1;
        const int secondPage = 2;
        for (var page = firstPage; page <= totalPages; page++)
        {
            var pageItems = items.Skip((page - firstPage) * pageSize).Take(pageSize).ToList();
            Uri? nextUrl = page < totalPages
                ? BuildRelativePaginationUrl(baseUrl, page + firstPage, basePath)
                : null;
            Uri? prevUrl;
            if (page == firstPage)
                prevUrl = null;
            else if (page == secondPage)
                prevUrl = BuildRelativePaginationUrl(baseUrl, 1, basePath);
            else
                prevUrl = BuildRelativePaginationUrl(baseUrl, page - firstPage, basePath);

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

    private static Uri BuildRelativePaginationUrl(string baseUrl, int pageNumber, string basePath)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        var pagePath = pageNumber <= 1
            ? normalizedBase
            : $"{normalizedBase}/page/{pageNumber}/";

        return new Uri(SiteConfiguration.ApplyBasePath(basePath, new Uri(pagePath, UriKind.Relative)), UriKind.Relative);
    }

    private static string ToOutputPath(Uri url, string basePath = "")
    {
        // /blog/hello-world/ → blog/hello-world/index.html
        var normalized = SiteConfiguration.RemoveBasePath(url, basePath).Trim('/');
        return string.IsNullOrEmpty(normalized)
            ? "index.html"
            : Path.Combine(normalized, "index.html");
    }

    private static Dictionary<string, ContentItem> BuildSlugIndex(ContentGroup targetCollection)
    {
        var index = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in targetCollection.Items)
            index.TryAdd(candidate.Slug, candidate);
        return index;
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

    private static async Task WriteOutputTextAsync(
        string outputPath,
        string content,
        HashSet<string>? generatedFiles,
        CancellationToken ct,
        Encoding? encoding = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (encoding is null)
            await File.WriteAllTextAsync(outputPath, content, ct).ConfigureAwait(false);
        else
            await File.WriteAllTextAsync(outputPath, content, encoding, ct).ConfigureAwait(false);

        generatedFiles?.Add(Path.GetFullPath(outputPath));
    }

    private static void CopyDirectory(string sourceDir, string destDir, HashSet<string>? generatedFiles)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
            generatedFiles?.Add(Path.GetFullPath(destPath));
        }
    }

    private void CopyDirectoryWithCollisionWarning(
        string sourceDir,
        string destDir,
        string themeName,
        Collection<string> warnings,
        HashSet<string>? generatedFiles,
        ImageCopyContext imageCopyContext)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);

            if (File.Exists(destPath))
                warnings.Add($"Asset '{relativePath}' in static/ overrides same file from theme '{themeName}'");

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            CopyOrOptimizeFile(file, destPath, imageCopyContext, warnings);
            generatedFiles?.Add(Path.GetFullPath(destPath));
        }
    }

    private void CopyNonMarkdownFiles(
        string sourceDir,
        string destDir,
        HashSet<string>? generatedFiles,
        ImageCopyContext imageCopyContext,
        Collection<string> warnings)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetExtension(file).Equals(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(destDir);
            var outputPath = Path.Combine(destDir, Path.GetFileName(file));
            CopyOrOptimizeFile(file, outputPath, imageCopyContext, warnings);
            generatedFiles?.Add(Path.GetFullPath(outputPath));
        }
    }

    /// <summary>
    /// Bundles the state needed to decide whether a candidate file (Site static/ or Page Bundle
    /// asset) should be optimized as an image: the output directory (for web-path comparisons
    /// against the set of referenced web paths built by <see cref="IAssetReferenceIndexBuilder"/>),
    /// the project path (cache location + exclude-glob base), the effective
    /// <see cref="ImageOptions"/>, the set of referenced image web paths, and the rename
    /// manifest for extension changes (e.g. WebP conversion).
    /// </summary>
    private sealed record ImageCopyContext(
        string OutputDir,
        string ProjectPath,
        ImageOptions ImageOptions,
        HashSet<string> ReferencedImages,
        Dictionary<string, string> RenameManifest);

    private void CopyOrOptimizeFile(string sourceFile, string destPath, ImageCopyContext context, Collection<string> warnings)
    {
        var ext = Path.GetExtension(sourceFile);
        var webPath = AssetPipeline.ToWebPath(context.OutputDir, destPath);
        var shouldOptimize = context.ImageOptions.Enabled
            && imageOptimizer.CanOptimize(ext)
            && context.ReferencedImages.Contains(webPath)
            && !ImageExcludeMatcher.IsExcluded(sourceFile, context.ProjectPath, context.ImageOptions.Exclude);

        if (!shouldOptimize)
        {
            File.Copy(sourceFile, destPath, overwrite: true);
            return;
        }

        try
        {
            var settings = new ImageOptimizationSettings(context.ImageOptions.MaxWidth, context.ImageOptions.Quality, context.ImageOptions.Webp);
            var (bytes, resultExt) = ImageOptimizationCache.GetOrOptimize(context.ProjectPath, sourceFile, settings, imageOptimizer);
            var finalDestPath = resultExt.Equals(ext, StringComparison.OrdinalIgnoreCase)
                ? destPath
                : Path.ChangeExtension(destPath, resultExt);

            File.WriteAllBytes(finalDestPath, bytes);
            if (!string.Equals(finalDestPath, destPath, StringComparison.OrdinalIgnoreCase))
                context.RenameManifest[webPath] = AssetPipeline.ToWebPath(context.OutputDir, finalDestPath);
        }
#pragma warning disable CA1031 // Intentional: a broken source image should not abort the entire build
        catch (Exception ex)
#pragma warning restore CA1031
        {
            warnings.Add($"Image optimization failed for '{sourceFile}': {ex.Message} — using unoptimized copy.");
            File.Copy(sourceFile, destPath, overwrite: true);
        }
    }

    private static void PruneStaleOutputs(string outputDir, HashSet<string> generatedFiles)
    {
        if (!Directory.Exists(outputDir))
            return;

        foreach (var file in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(file);
            if (!generatedFiles.Contains(fullPath))
                File.Delete(fullPath);
        }

        foreach (var directory in Directory.GetDirectories(outputDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static path => path.Length))
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
                continue;
            Directory.Delete(directory);
        }
    }
}
