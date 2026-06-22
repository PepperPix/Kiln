namespace Kiln.Services;

using Kiln.Abstractions;

public sealed class NoOpAssetMinifier : IAssetMinifier
{
    public string Id => "noop";

    public bool CanMinify(AssetType type) => true;

    public string Minify(string content, AssetType type) => content;
}
