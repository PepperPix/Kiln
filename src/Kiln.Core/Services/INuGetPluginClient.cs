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

    /// <summary>
    /// Describes a package (trust level, publisher, contents, external hosts) without installing it.
    /// Implementations that predate this member do not support it.
    /// </summary>
    Task<PluginPackageInfo> GetInfoAsync(string packageId, string? version, CancellationToken ct = default)
        => throw new NotSupportedException($"{GetType().Name} does not support package inspection.");
}
