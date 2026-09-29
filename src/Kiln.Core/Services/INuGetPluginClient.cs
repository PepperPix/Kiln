namespace Kiln.Services;

public interface INuGetPluginClient
{
    Task<IReadOnlyList<PluginSearchResult>> SearchAsync(string query, CancellationToken ct = default);
    Task<string?> GetLatestVersionAsync(string packageId, CancellationToken ct = default);
    Task<bool> IsUpdateAvailableAsync(string packageId, string currentVersion, CancellationToken ct = default);
    Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, CancellationToken ct = default);

    /// <summary>
    /// Installs a package with explicit <paramref name="options"/>. Implementations that predate the options
    /// fall back to the overload without options.
    /// </summary>
    Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, PluginInstallOptions options, CancellationToken ct = default)
        => AddAsync(packageId, version, projectPath, ct);
}
