namespace Kiln.Services;

using Kiln.Models;

public sealed record PluginPackageInstallResult(string PackageId, string Version, string PluginName, string InstallPath)
{
    /// <summary>
    /// Gets the SHA-256 content hash of the installed plugin directory, if computed.
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// Gets the trust level of the installed package.
    /// </summary>
    public PluginTrustLevel Trust { get; init; } = PluginTrustLevel.Community;
}
