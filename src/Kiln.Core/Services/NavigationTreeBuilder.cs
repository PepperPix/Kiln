namespace Kiln.Services;

using System.Globalization;
using System.Text.RegularExpressions;
using Kiln.Models;

public static class NavigationTreeBuilder
{
    public static IReadOnlyDictionary<string, IReadOnlyList<NavigationNode>> Build(
        IReadOnlyList<ContentItem> publishedItems,
        string basePath = "")
    {
        var byCollection = publishedItems
            .GroupBy(i => i.Collection.Name);

        var result = new Dictionary<string, IReadOnlyList<NavigationNode>>();

        foreach (var group in byCollection)
        {
            var rootNodes = new List<NavigationNode>();
            var sectionLookup = new Dictionary<string, List<NavigationNode>>(StringComparer.Ordinal);

            foreach (var item in group.OrderBy(i => i.Weight).ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase))
            {
                var segs = string.IsNullOrEmpty(item.SectionPath)
                    ? []
                    : item.SectionPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                var leaf = new NavigationNode
                {
                    Title = item.Title,
                    Url = new Uri(SiteConfiguration.ApplyBasePath(basePath, item.Url), UriKind.Relative),
                    Weight = item.Weight
                };

                if (segs.Length == 0)
                {
                    rootNodes.Add(leaf);
                }
                else
                {
                    var fullPath = string.Join("/", segs);
                    if (!sectionLookup.TryGetValue(fullPath, out var list))
                    {
                        list = [];
                        sectionLookup[fullPath] = list;
                    }
                    list.Add(leaf);
                }
            }

            // Create section nodes from lookup
            var sectionNodes = new Dictionary<string, NavigationNode>(StringComparer.Ordinal);
            var sectionChildren = new Dictionary<string, List<NavigationNode>>(StringComparer.Ordinal);

            // First pass: create section nodes
            foreach (var sectionPath in sectionLookup.Keys)
            {
                EnsureSectionNode(sectionPath, sectionNodes, sectionChildren, group.First(), basePath);
            }

            // Second pass: assign leaf children
            foreach (var (sectionPath, leaves) in sectionLookup)
            {
                if (sectionChildren.TryGetValue(sectionPath, out var children))
                {
                    children.AddRange(leaves);
                }
            }

            // Sort all section children
            foreach (var list in sectionChildren.Values)
            {
                list.Sort((a, b) =>
                {
                    var cmp = a.Weight.CompareTo(b.Weight);
                    return cmp != 0 ? cmp : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
                });
            }

            // Attach section nodes to their parents (or root)
            foreach (var (sectionPath, node) in sectionNodes)
            {
                var lastSlash = sectionPath.LastIndexOf('/');
                if (lastSlash < 0)
                {
                    rootNodes.Add(node);
                }
                else
                {
                    var parentPath = sectionPath[..lastSlash];
                    if (sectionChildren.TryGetValue(parentPath, out var parentList))
                    {
                        parentList.Add(node);
                    }
                }
            }

            // Re-sort children lists after section nodes have been added
            foreach (var list in sectionChildren.Values)
            {
                list.Sort((a, b) =>
                {
                    var cmp = a.Weight.CompareTo(b.Weight);
                    return cmp != 0 ? cmp : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
                });
            }

            // Assign children lists to section nodes (children were accumulated in sectionChildren)
            foreach (var (sectionPath, node) in sectionNodes)
            {
                if (sectionChildren.TryGetValue(sectionPath, out var children))
                {
                    node.Children.AddRange(children);
                }
            }

            // Sort root nodes
            rootNodes.Sort((a, b) =>
            {
                var cmp = a.Weight.CompareTo(b.Weight);
                return cmp != 0 ? cmp : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            });

            result[group.Key] = rootNodes;
        }

        return result;
    }

    private static void EnsureSectionNode(
        string sectionPath,
        Dictionary<string, NavigationNode> sectionNodes,
        Dictionary<string, List<NavigationNode>> sectionChildren,
        ContentItem sampleItem,
        string basePath)
    {
        if (sectionNodes.ContainsKey(sectionPath))
            return;

        var segs = sectionPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var url = BuildSectionUrl(sampleItem, segs, basePath);
        var title = Humanize(segs[^1]);

        var node = new NavigationNode
        {
            Title = title,
            Url = url,
            Weight = 0
        };

        sectionNodes[sectionPath] = node;
        sectionChildren[sectionPath] = [];

        // Ensure parent section exists recursively
        if (segs.Length > 1)
        {
            var parentPath = string.Join("/", segs[..^1]);
            EnsureSectionNode(parentPath, sectionNodes, sectionChildren, sampleItem, basePath);
        }
    }

    private static Uri BuildSectionUrl(ContentItem sampleItem, string[] sectionSegs, string basePath)
    {
        var sep = Path.AltDirectorySeparatorChar;
        var siteRelativeUrl = SiteConfiguration.RemoveBasePath(sampleItem.Url, basePath);
        var urlSegs = siteRelativeUrl.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        var sectionSegments = string.Join(sep, sectionSegs).Split(sep, StringSplitOptions.RemoveEmptyEntries);

        var itemSectionSegs = string.IsNullOrEmpty(sampleItem.SectionPath)
            ? []
            : sampleItem.SectionPath.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        var prefixCount = urlSegs.Length - itemSectionSegs.Length - 1;
        var prefixSegs = prefixCount > 0 ? urlSegs[..prefixCount] : [];

        var combined = new List<string>(prefixSegs);
        combined.AddRange(sectionSegments);

        var urlStr = sep + string.Join(sep, combined) + sep;
        return new Uri(SiteConfiguration.ApplyBasePath(basePath, new Uri(urlStr, UriKind.Relative)), UriKind.Relative);
    }

    internal static string Humanize(string segment)
    {
        var withSpaces = Regex.Replace(segment, @"[-_]", " ");
        if (string.IsNullOrWhiteSpace(withSpaces))
            return segment;

        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        return textInfo.ToTitleCase(withSpaces);
    }
}
