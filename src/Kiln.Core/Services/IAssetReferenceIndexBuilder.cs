namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Builds an index of which content items reference which <c>/assets/...</c> web paths, by
/// scanning already-rendered <see cref="ContentItem.HtmlContent"/> for <c>img</c> tag references.
/// </summary>
public interface IAssetReferenceIndexBuilder
{
    /// <summary>
    /// Scans <paramref name="items"/> for <c>&lt;img src="/assets/..."&gt;</c> references and
    /// returns a map from each referenced web path to the list of content items referencing it.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<ContentItemRef>> Build(IReadOnlyList<ContentItem> items);
}
