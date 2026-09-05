namespace Kiln.Services;

using System.Text.Json.Serialization;

public sealed record PluginLockEntry(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("source")] string Source);
