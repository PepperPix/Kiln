namespace Kiln.Services;

using System.Text.Json.Serialization;
using Kiln.Models;

/// <summary>
/// An entry of the plugin lock file that records an installed plugin package.
/// </summary>
/// <param name="PackageId">The NuGet package ID.</param>
/// <param name="Version">The installed package version.</param>
/// <param name="Source">The kind of source the package was installed from, for example <c>nuget</c>.</param>
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

    /// <summary>
    /// Gets the trust level recorded at install time; <c>null</c> for lock files written before trust levels were recorded (treated as community).
    /// </summary>
    [JsonPropertyName("trust")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PluginTrustLevel? Trust { get; init; }
}
