namespace Kiln.Services;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Kiln.Abstractions;
using Kiln.Models;

#pragma warning disable S107 // 8 DI-injected services; no sensible split without a facade.
/// <summary>
/// Default <see cref="ISiteBuilder"/> implementation that reads the content, renders the pages and runs the asset pipeline.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on <see cref="ISiteBuilder"/> instead.
/// </remarks>
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
    private readonly SiteAssetCopier _assetCopier = new(imageOptimizer, assetReferenceIndexBuilder);

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
        SiteReferenceResolver.ResolveCrossCollectionReferences(config, warnings);

        // Extract taxonomy terms (aggregate across all collections)
        var allTaxonomyTerms = VirtualPageCatalog.ExtractTaxonomyTerms(config, includeDrafts);

        // Build navigation tree once per build
        var publishedItems = allItems.Where(i => !i.Draft || includeDrafts).ToList();
        var navTree = NavigationTreeBuilder.Build(publishedItems, config.BasePath);
        var sharedRenderContext = SharedRenderContext.Build(config, allTaxonomyTerms, navTree);

        // Collect all virtual page URLs for collision checking
        var virtualUrls = VirtualPageCatalog.CollectVirtualUrls(config, allTaxonomyTerms, includeDrafts);

        // Permalink collision check: content items + virtual pages
        CheckPermalinkCollisions(allItems, virtualUrls, errors);

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        // Resolve menu references
        SiteReferenceResolver.ResolveMenuRefs(config, allItems, virtualUrls, warnings, errors);

        if (errors.Count > 0)
            return MakeResult(allItems.Count, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);

        var useWriteThenPrune = environment == BuildEnvironment.Development;
        var generatedFiles = useWriteThenPrune
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : null;

        // For development builds, write-then-prune avoids transient 404 windows during serve.
        OutputDirectoryGuard.EnsureNotProjectRootOrAncestor(projectPath, outputDir);
        if (!useWriteThenPrune && Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
        Directory.CreateDirectory(outputDir);

        var render = new RenderPassContext(sharedRenderContext, config, themePath, plugins, outputDir, generatedFiles);

        // Render content items
        var (rendered, skippedDrafts) = await RenderContentItemsAsync(
            allItems, includeDrafts, render, warnings, progress, errors, ct).ConfigureAwait(false);

        // Render collection index pages (for collections with Paginate > 0)
        rendered += await RenderCollectionIndexesAsync(includeDrafts, render, errors, ct).ConfigureAwait(false);

        // Render taxonomy term and overview pages
        rendered += await RenderTaxonomyPagesAsync(allTaxonomyTerms, render, errors, ct).ConfigureAwait(false);

        // Emit a 404 page only when the theme provides a dedicated '404.html' layout.
        await RenderNotFoundPageAsync(render, errors, ct).ConfigureAwait(false);

        // Copy assets (theme → site → page bundles → plugins), then rewrite renamed image refs
        _assetCopier.Copy(allItems, projectPath, environment, warnings, render);

        // Generate sitemap.xml, Atom feeds, robots.txt
        await WriteFeedsAndMetaAsync(config, allItems, allTaxonomyTerms, includeDrafts, outputDir, generatedFiles, ct).ConfigureAwait(false);

        // Production asset pipeline: minify → fingerprint → link-check
        var tally = new BuildTally(allItems.Count, rendered, skippedDrafts, stopwatch.Elapsed);
        var earlyResult = await RunProductionAssetPipelineAsync(config, environment, outputDir, tally, warnings, errors, ct).ConfigureAwait(false);
        if (earlyResult is not null)
            return earlyResult;

        if (generatedFiles is not null)
            BuildOutputFiles.PruneStaleOutputs(projectPath, outputDir, generatedFiles);

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

        if (OutputDirectoryGuard.Validate(projectPath, config) is { } outputDirError)
        {
            errors.Add(outputDirError);
            return false;
        }

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

    private async Task<(int Rendered, int SkippedDrafts)> RenderContentItemsAsync(
        List<ContentItem> allItems,
        bool includeDrafts,
        RenderPassContext render,
        Collection<string> warnings,
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
                if (item.NoIndex)
                {
                    var originalHtml = html;
                    html = InjectNoIndexMeta(html);
                    if (html == originalHtml)
                        warnings.Add($"'{item.RelativePath}' has noIndex: true but no </head> tag was found; robots meta tag was not injected.");
                }

                var outputPath = Path.Combine(render.OutputDir, item.OutputPath);
                await BuildOutputFiles.WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
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

    private static string InjectNoIndexMeta(string html)
    {
        const string metaTag = "<meta name=\"robots\" content=\"noindex, nofollow\">";
        var headEndIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return headEndIndex < 0
            ? html
            : html.Insert(headEndIndex, metaTag);
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

            var paginators = VirtualPageCatalog.BuildPaginators(nonDraftItems, collection.Paginate!.Value, collection.IndexUrl.OriginalString, render.Config.BasePath);
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
                    await BuildOutputFiles.WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
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
                await BuildOutputFiles.WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
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
                    ? VirtualPageCatalog.BuildPaginators(term.Items.ToList(), taxDef.Paginate!.Value, term.Url.OriginalString, render.Config.BasePath)
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
                        await BuildOutputFiles.WriteOutputTextAsync(outputPath, html, render.GeneratedFiles, ct).ConfigureAwait(false);
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
            await BuildOutputFiles.WriteOutputTextAsync(notFoundPath, notFoundHtml, render.GeneratedFiles, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Intentional: a 404 page rendering error should not abort the entire build
        catch (Exception ex)
#pragma warning restore CA1031
        {
            errors.Add($"Error rendering not-found page: {ex.Message}");
        }
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
        await BuildOutputFiles.WriteOutputTextAsync(Path.Combine(outputDir, "sitemap.xml"), sitemapContent, generatedFiles, ct, Encoding.UTF8).ConfigureAwait(false);

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
            await BuildOutputFiles.WriteOutputTextAsync(Path.Combine(feedDir, "feed.xml"), feedContent, generatedFiles, ct, Encoding.UTF8).ConfigureAwait(false);
        }

        // Generate robots.txt
        var robotsTxt = $"User-agent: *\nAllow: /\n\nSitemap: {config.BaseUrl.ToString().TrimEnd('/')}/sitemap.xml\n";
        await BuildOutputFiles.WriteOutputTextAsync(Path.Combine(outputDir, "robots.txt"), robotsTxt, generatedFiles, ct, Encoding.UTF8).ConfigureAwait(false);
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

    private static string ToOutputPath(Uri url, string basePath = "")
    {
        // /blog/hello-world/ → blog/hello-world/index.html
        var normalized = SiteConfiguration.RemoveBasePath(url, basePath).Trim('/');
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
}
