namespace Kiln.Services;

public sealed record PluginPackageInstallResult(string PackageId, string Version, string PluginName, string InstallPath)
{
    /// <summary>
    /// Gets the SHA-256 content hash of the installed plugin directory, if computed.
    /// </summary>
    public string? ContentHash { get; init; }
}
