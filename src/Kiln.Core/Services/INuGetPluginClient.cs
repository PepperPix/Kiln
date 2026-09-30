namespace Kiln.Services;

/// <summary>
/// Finds, inspects and installs plugin packages from a NuGet feed.
/// </summary>
public interface INuGetPluginClient
{
    /// <summary>
    /// Searches for plugin packages, which are the packages tagged <c>kiln-plugin</c>.
    /// </summary>
    /// <param name="query">Additional search text; when blank, all plugin packages are searched.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The matching stable packages.</returns>
    Task<IReadOnlyList<PluginSearchResult>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest stable version of a package.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The version, or <see langword="null"/> when the package has no stable version.</returns>
    Task<string?> GetLatestVersionAsync(string packageId, CancellationToken ct = default);

    /// <summary>
    /// Determines whether a newer stable version of a package exists.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="currentVersion">The installed version.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the latest stable version is greater than <paramref name="currentVersion"/>;
    /// <see langword="false"/> if it is not, or if either version cannot be determined.
    /// </returns>
    Task<bool> IsUpdateAvailableAsync(string packageId, string currentVersion, CancellationToken ct = default);

    /// <summary>
    /// Installs a plugin package into the <c>plugins</c> directory of a project.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The version to install, or <see langword="null"/> for the latest stable version.</param>
    /// <param name="projectPath">The project directory.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The result of the installation.</returns>
    /// <exception cref="InvalidOperationException">The package cannot be found or installed as a plugin.</exception>
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
