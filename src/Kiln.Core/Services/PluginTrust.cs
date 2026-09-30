namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Classifies plugin packages into trust levels.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on the interfaces in Kiln.Abstractions and Kiln.Services instead.
/// </remarks>
public static class PluginTrust
{
    /// <summary>
    /// The package ID prefix that is reserved for first-party plugins.
    /// </summary>
    public const string FirstPartyPrefix = "Kiln.Plugin.";

    /// <summary>
    /// Returns <see cref="PluginTrustLevel.FirstParty"/> only if <paramref name="packageId"/> starts with
    /// <see cref="FirstPartyPrefix"/> (case-insensitive) and the feed reports the prefix as reserved;
    /// otherwise <see cref="PluginTrustLevel.Community"/>.
    /// </summary>
    public static PluginTrustLevel Classify(string packageId, bool prefixReserved)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        return prefixReserved && packageId.StartsWith(FirstPartyPrefix, StringComparison.OrdinalIgnoreCase)
            ? PluginTrustLevel.FirstParty
            : PluginTrustLevel.Community;
    }
}
