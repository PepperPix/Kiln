namespace Kiln.Abstractions;

/// <summary>
/// The input handed to an <see cref="IAssetProcessor"/>.
/// </summary>
/// <param name="Asset">The asset to process.</param>
/// <param name="OutputDir">The output directory of the site being built.</param>
public sealed record AssetContext(Asset Asset, string OutputDir);
