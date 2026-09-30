namespace Kiln.Abstractions;

/// <summary>
/// A site asset (stylesheet, script, markup, image source or other file) passed through asset processing.
/// </summary>
/// <param name="RelativePath">The relative path of the asset.</param>
/// <param name="Type">The kind of asset.</param>
/// <param name="Content">The raw bytes of the asset.</param>
public sealed record Asset(string RelativePath, AssetType Type, ReadOnlyMemory<byte> Content);
