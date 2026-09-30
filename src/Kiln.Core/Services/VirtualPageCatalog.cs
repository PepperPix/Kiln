namespace Kiln.Services;

using Kiln.Models;

internal static class VirtualPageCatalog
{
    internal static Dictionary<string, IReadOnlyList<TaxonomyTerm>> ExtractTaxonomyTerms(
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

    internal static List<string> CollectVirtualUrls(
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

    internal static List<Paginator> BuildPaginators(
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
}
