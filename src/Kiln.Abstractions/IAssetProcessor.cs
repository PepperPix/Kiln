namespace Kiln.Abstractions;

public interface IAssetProcessor
{
    int Order { get; }

    Asset Process(AssetContext context);
}
