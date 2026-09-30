namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Generates the URL of a content item.
/// </summary>
public interface IPermalinkGenerator
{
    /// <summary>
    /// Generates the relative URL of an item. A URL set in the item's front matter takes precedence;
    /// otherwise the permalink pattern of the collection is applied.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="collection">The collection of the item.</param>
    /// <param name="basePath">The base path to prefix, or <see langword="null"/> for none.</param>
    /// <returns>The relative URL.</returns>
    Uri Generate(ContentItem item, ContentGroup collection, string? basePath = null);
}
