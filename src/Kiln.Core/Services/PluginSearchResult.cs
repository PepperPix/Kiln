namespace Kiln.Services;

using Kiln.Models;

public sealed record PluginSearchResult(string Id, string Version, string Description)
{
    /// <summary>
    /// Gets the trust level derived from the package ID and the feed's prefix reservation.
    /// </summary>
    public PluginTrustLevel Trust { get; init; } = PluginTrustLevel.Community;
}
