namespace Kiln.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Describes how much a plugin package can be trusted based on its origin.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PluginTrustLevel>))]
public enum PluginTrustLevel
{
    /// <summary>
    /// The package carries the <c>kiln-plugin</c> tag but its publisher is not verified by a reserved ID prefix.
    /// </summary>
    [JsonStringEnumMemberName("community")]
    Community,

    /// <summary>
    /// The package ID uses the reserved <c>Kiln.Plugin.</c> prefix and the feed confirms the reservation.
    /// </summary>
    [JsonStringEnumMemberName("firstParty")]
    FirstParty,
}
