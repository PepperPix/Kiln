namespace Kiln.Abstractions;

public sealed record Asset(string RelativePath, AssetType Type, ReadOnlyMemory<byte> Content);
