namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// A plugin package found by a search.
/// </summary>
/// <param name="Id">The NuGet package ID.</param>
/// <param name="Version">The package version.</param>
/// <param name="Description">The package description.</param>
public sealed record PluginSearchResult(string Id, string Version, string Description)
{
    /// <summary>
    /// Gets the trust level derived from the package ID and the feed's prefix reservation.
    /// </summary>
    public PluginTrustLevel Trust { get; init; } = PluginTrustLevel.Community;
}
