namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// The outcome of installing a plugin package.
/// </summary>
/// <param name="PackageId">The NuGet package ID.</param>
/// <param name="Version">The installed package version.</param>
/// <param name="PluginName">The plugin name, which is also the name of the installation directory.</param>
/// <param name="InstallPath">The full path of the installation directory.</param>
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
