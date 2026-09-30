namespace Kiln.Services;

using Kiln.Abstractions;

/// <summary>
/// An <see cref="IAssetMinifier"/> with the identifier <c>noop</c> that returns the content unchanged.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on <see cref="IAssetMinifier"/> instead.
/// </remarks>
public sealed class NoOpAssetMinifier : IAssetMinifier
{
    public string Id => "noop";

    public bool CanMinify(AssetType type) => true;

    public string Minify(string content, AssetType type) => content;
}
