namespace Kiln.Services;

using System.Text.Json.Serialization;

public sealed record PluginLockEntry(
    [property: JsonPropertyName("packageId")] string PackageId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("source")] string Source)
{
    /// <summary>
    /// Gets the SHA-256 content hash of the installed plugin directory; <c>null</c> for lock files written before hashes were recorded.
    /// </summary>
    [JsonPropertyName("contentHash")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentHash { get; init; }
}
