namespace Kiln.Services;

using System.Text.RegularExpressions;
using Kiln.Models;

/// <summary>
/// Default <see cref="IAssetReferenceIndexBuilder"/> implementation. Scans already-rendered
/// <see cref="ContentItem.HtmlContent"/> for image references (an <c>img</c> element's <c>src</c>
/// attribute), honoring the per-item <see cref="ContentItem.ImageOptimization"/> opt-out. Both
/// Page-Bundle images (relative <c>assetBasePath</c> resolved by <see cref="IMarkdownProcessor.ToHtml"/>)
/// and Site-<c>static/</c>-referenced images already use the <c>/assets/</c> convention by the
/// time HtmlContent is built, so no separate Markdown parsing is needed here.
/// </summary>
public sealed partial class AssetReferenceIndexBuilder : IAssetReferenceIndexBuilder
{
    public IReadOnlyDictionary<string, IReadOnlyList<ContentItemRef>> Build(IReadOnlyList<ContentItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var refsByPath = new Dictionary<string, List<ContentItemRef>>(StringComparer.OrdinalIgnoreCase);
        var seenSourcePathsByPath = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!item.ImageOptimization) continue;

            foreach (Match match in ImgSrcRegex().Matches(item.HtmlContent))
            {
                var src = match.Groups[1].Value;
                if (!src.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!refsByPath.TryGetValue(src, out var refs))
                {
                    refs = [];
                    refsByPath[src] = refs;
                    seenSourcePathsByPath[src] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                // Collapse multiple <img> tags in the same item pointing at the same path down
                // to a single ContentItemRef — callers care about "which items reference this
                // asset", not "how many times".
                if (seenSourcePathsByPath[src].Add(item.SourcePath))
                    refs.Add(new ContentItemRef(item.Title, item.SourcePath, item.Collection.Name));
            }
        }

        var result = new Dictionary<string, IReadOnlyList<ContentItemRef>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, refs) in refsByPath)
            result[path] = refs;

        return result;
    }

    [GeneratedRegex(
        "<img\\b[^>]*\\bsrc=\"([^\"]+)\"",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcRegex();
}
