namespace Kiln.Abstractions;

public interface IKilnBuilder
{
    IKilnBuilder UseMinifier<T>() where T : class, IAssetMinifier;

    IKilnBuilder AddAssetProcessor<T>() where T : class, IAssetProcessor;
}
