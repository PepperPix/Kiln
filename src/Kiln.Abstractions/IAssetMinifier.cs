namespace Kiln.Abstractions;

public interface IAssetMinifier
{
    string Id { get; }

    bool CanMinify(AssetType type);

    string Minify(string content, AssetType type);
}
