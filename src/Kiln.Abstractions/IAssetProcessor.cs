namespace Kiln.Abstractions;

public sealed record Asset(string RelativePath, AssetType Type, ReadOnlyMemory<byte> Content);

public sealed record AssetContext(Asset Asset, string OutputDir);

public interface IAssetProcessor
{
    int Order { get; }

    Asset Process(AssetContext context);
}
