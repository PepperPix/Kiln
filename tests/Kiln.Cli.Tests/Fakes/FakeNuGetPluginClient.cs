namespace Kiln.Cli.Tests.Fakes;

using Kiln.Models;
using Kiln.Services;

public sealed class FakeNuGetPluginClient : INuGetPluginClient
{
    public IReadOnlyList<PluginSearchResult> SearchResults { get; set; } = [];

    public string? LatestVersion { get; set; }

    public PluginPackageInstallResult? InstallResult { get; set; }

    public int AddCallCount { get; private set; }

    public string? LastAddVersion { get; private set; }

    public PluginInstallOptions? LastOptions { get; private set; }

    public Exception? AddException { get; set; }

    /// <summary>
    /// Gets or sets the trust level reported for packages when no explicit result is configured.
    /// </summary>
    public PluginTrustLevel DefaultTrust { get; set; } = PluginTrustLevel.FirstParty;

    public PluginPackageInfo? InfoResult { get; set; }

    public Exception? InfoException { get; set; }

    /// <summary>
    /// Gets or sets a factory that builds the info per package ID and version; used when <see cref="InfoResult"/> is not set.
    /// </summary>
    public Func<string, string, PluginPackageInfo>? InfoFactory { get; set; }

    public int InfoCallCount { get; private set; }

    public string? LastInfoVersion { get; private set; }

    public Task<PluginPackageInfo> GetInfoAsync(string packageId, string? version, CancellationToken ct = default)
    {
        InfoCallCount++;
        LastInfoVersion = version;
        if (InfoException is not null)
            throw InfoException;

        var resolvedVersion = version ?? "1.0.0";
        return Task.FromResult(InfoResult ?? InfoFactory?.Invoke(packageId, resolvedVersion) ?? PluginInfoFactory.Create(packageId, resolvedVersion, DefaultTrust));
    }

    public Task<IReadOnlyList<PluginSearchResult>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult(SearchResults);

    public Task<string?> GetLatestVersionAsync(string packageId, CancellationToken ct = default)
        => Task.FromResult(LatestVersion);

    public Task<bool> IsUpdateAvailableAsync(string packageId, string currentVersion, CancellationToken ct = default)
        => Task.FromResult(!string.Equals(LatestVersion, currentVersion, StringComparison.OrdinalIgnoreCase));

    public Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, PluginInstallOptions options, CancellationToken ct = default)
    {
        LastOptions = options;
        return AddAsync(packageId, version, projectPath, ct);
    }

    public Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, CancellationToken ct = default)
    {
        AddCallCount++;
        LastAddVersion = version;
        if (AddException is not null)
            throw AddException;

        var pluginName = packageId.Split('.').Last();
        var installPath = Path.Combine(projectPath, "plugins", pluginName);

        return Task.FromResult(
            InstallResult ?? new PluginPackageInstallResult(packageId, version ?? "1.0.0", pluginName, installPath) { Trust = DefaultTrust });
    }
}
