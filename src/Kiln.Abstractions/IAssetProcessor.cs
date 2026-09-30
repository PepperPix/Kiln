namespace Kiln.Abstractions;

/// <summary>
/// A custom processing step for an <see cref="Asset"/>, registered through <see cref="IKilnBuilder.AddAssetProcessor{T}"/>.
/// </summary>
public interface IAssetProcessor
{
    /// <summary>
    /// Gets the order value of this processor among the registered processors.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Processes an asset.
    /// </summary>
    /// <param name="context">The asset and the site output directory.</param>
    /// <returns>The resulting asset.</returns>
    Asset Process(AssetContext context);
}
