namespace Kiln.Models;

/// <summary>
/// A content item that references an asset, as reported by
/// <see cref="Kiln.Services.IAssetReferenceIndexBuilder"/>.
/// </summary>
/// <param name="Title">The item's title (for UI display).</param>
/// <param name="SourcePath">Absolute path to the item's source Markdown file (for opening it in an editor).</param>
/// <param name="CollectionName">Name of the collection (<see cref="ContentGroup.Name"/>) the item belongs to.</param>
public sealed record ContentItemRef(string Title, string SourcePath, string CollectionName);
