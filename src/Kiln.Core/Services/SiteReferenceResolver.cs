namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Models;

internal static class SiteReferenceResolver
{
    internal static void ResolveCrossCollectionReferences(SiteConfiguration config, Collection<string> warnings)
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

    internal static void ResolveMenuRefs(
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

    private static Dictionary<string, ContentItem> BuildSlugIndex(ContentGroup targetCollection)
    {
        var index = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in targetCollection.Items)
            index.TryAdd(candidate.Slug, candidate);
        return index;
    }
}
