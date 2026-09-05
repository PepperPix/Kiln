namespace Kiln.Services;

using System.IO.Compression;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public sealed class NuGetPluginClient : INuGetPluginClient
{
    private const string DefaultServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private readonly SourceRepository _sourceRepository;

    public NuGetPluginClient()
        : this(DefaultServiceIndexUrl)
    {
    }

    public NuGetPluginClient(string serviceIndexUrl)
    {
        var value = string.IsNullOrWhiteSpace(serviceIndexUrl)
            ? throw new ArgumentException("Service index URL cannot be empty.", nameof(serviceIndexUrl))
            : serviceIndexUrl;

        _sourceRepository = Repository.Factory.GetCoreV3(value);
    }

    public NuGetPluginClient(Uri serviceIndexUrl)
    {
        ArgumentNullException.ThrowIfNull(serviceIndexUrl);
        _sourceRepository = Repository.Factory.GetCoreV3(serviceIndexUrl.ToString());
    }

    public NuGetPluginClient(string serviceIndexUrl, HttpMessageHandler? httpMessageHandler)
        : this(serviceIndexUrl)
    {
        _ = httpMessageHandler;
    }

    public NuGetPluginClient(Uri serviceIndexUrl, HttpMessageHandler? httpMessageHandler)
        : this(serviceIndexUrl)
    {
        _ = httpMessageHandler;
    }

    public NuGetPluginClient(SourceRepository sourceRepository)
    {
        _sourceRepository = sourceRepository ?? throw new ArgumentNullException(nameof(sourceRepository));
    }

    public async Task<IReadOnlyList<PluginSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var packageSearch = await _sourceRepository.GetResourceAsync<PackageSearchResource>(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The NuGet repository does not provide package search support.");
        var searchText = string.IsNullOrWhiteSpace(query)
            ? "tags:kiln-plugin"
            : $"{query} tags:kiln-plugin";

        var searchResults = await packageSearch.SearchAsync(
            searchText,
            new SearchFilter(includePrerelease: false),
            skip: 0,
            take: 50,
            NullLogger.Instance,
            ct).ConfigureAwait(false);

        var results = new List<PluginSearchResult>();
        foreach (var item in searchResults)
        {
            var identity = item.Identity;
            if (identity is null)
                continue;

            var tagsText = item.Tags ?? string.Empty;
            var tagNames = tagsText.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!Array.Exists(tagNames, tag => string.Equals(tag, "kiln-plugin", StringComparison.OrdinalIgnoreCase)))
                continue;

            var versionText = identity.Version.OriginalVersion ?? string.Empty;
            results.Add(new PluginSearchResult(identity.Id, versionText, item.Description ?? string.Empty));
        }

        return results;
    }

    public Task<string?> GetLatestVersionAsync(string packageId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        var normalizedPackageId = NormalizePackageId(packageId);
        return GetLatestVersionCoreAsync(normalizedPackageId, ct);
    }

    public Task<bool> IsUpdateAvailableAsync(string packageId, string currentVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(currentVersion);
        return IsUpdateAvailableCoreAsync(packageId, currentVersion, ct);
    }

    public Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(projectPath);

        var normalizedPackageId = NormalizePackageId(packageId);
        return AddCoreAsync(normalizedPackageId, version, projectPath, ct);
    }

    private async Task<string?> GetLatestVersionCoreAsync(string normalizedPackageId, CancellationToken ct)
    {
        var packageFinder = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The NuGet repository does not provide package lookup support.");

        using var cacheContext = new SourceCacheContext();
        var versions = await packageFinder.GetAllVersionsAsync(
            normalizedPackageId,
            cacheContext,
            NullLogger.Instance,
            ct).ConfigureAwait(false);

        var latestVersion = versions
            .Where(v => !v.IsPrerelease)
            .DefaultIfEmpty()
            .Max();

        return latestVersion?.OriginalVersion;
    }

    private async Task<bool> IsUpdateAvailableCoreAsync(string packageId, string currentVersion, CancellationToken ct)
    {
        var latestVersionText = await GetLatestVersionAsync(packageId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(latestVersionText) || !NuGetVersion.TryParse(latestVersionText, out var latestVersion))
            return false;

        if (!NuGetVersion.TryParse(currentVersion, out var installedVersion))
            return false;

        return latestVersion > installedVersion;
    }

    private async Task<PluginPackageInstallResult> AddCoreAsync(string normalizedPackageId, string? version, string projectPath, CancellationToken ct)
    {
        var resolvedVersion = string.IsNullOrWhiteSpace(version)
            ? await GetLatestVersionCoreAsync(normalizedPackageId, ct).ConfigureAwait(false)
            : version;

        if (string.IsNullOrWhiteSpace(resolvedVersion))
            throw new InvalidOperationException($"Package '{normalizedPackageId}' has no published stable {nameof(version)}.");

        if (!NuGetVersion.TryParse(resolvedVersion, out var nuGetVersion))
            throw new InvalidOperationException($"Package '{normalizedPackageId}' requested {nameof(version)} '{resolvedVersion}' is not a valid NuGet {nameof(version)}.");

        var packageFinder = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The NuGet repository does not provide package lookup support.");
        using var packageStream = new MemoryStream();
        using var cacheContext = new SourceCacheContext();
        await packageFinder.CopyNupkgToStreamAsync(
            normalizedPackageId,
            nuGetVersion,
            packageStream,
            cacheContext,
            NullLogger.Instance,
            ct).ConfigureAwait(false);

        packageStream.Position = 0;

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-plugin-{Guid.NewGuid():N}");
        var contentRoot = Path.Combine(tempRoot, "content");
        Directory.CreateDirectory(contentRoot);

        try
        {
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("content/", StringComparison.OrdinalIgnoreCase)))
            {
                var relativePath = entry.FullName["content/".Length..];
                if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Equals(".", StringComparison.Ordinal))
                    continue;

                var destinationPath = Path.GetFullPath(Path.Combine(contentRoot, relativePath));
                var destinationRoot = Path.GetFullPath(contentRoot);
                if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Archive entry '{entry.FullName}' is outside the plugin content directory.");

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                await entry.ExtractToFileAsync(destinationPath, overwrite: true, ct).ConfigureAwait(false);
            }

            var pluginManifestPath = FindManifestPath(contentRoot);
            if (pluginManifestPath is null)
                throw new InvalidOperationException($"Package '{normalizedPackageId}' does not contain a plugin.yaml manifest.");

            var pluginName = ReadPluginName(pluginManifestPath);
            var projectRoot = Path.GetFullPath(projectPath);
            var destinationDir = Path.Combine(projectRoot, "plugins", pluginName);
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.EnumerateFiles(contentRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(contentRoot, file);
                var targetPath = Path.Combine(destinationDir, relativePath);
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                File.Copy(file, targetPath, overwrite: true);
            }

            return new PluginPackageInstallResult(
                normalizedPackageId,
                resolvedVersion,
                pluginName,
                destinationDir);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string? FindManifestPath(string contentRoot)
    {
        var yamlPath = Path.Combine(contentRoot, "plugin.yaml");
        if (File.Exists(yamlPath))
            return yamlPath;

        var ymlPath = Path.Combine(contentRoot, "plugin.yml");
        return File.Exists(ymlPath) ? ymlPath : null;
    }

    private static string ReadPluginName(string manifestPath)
    {
        var contents = File.ReadAllText(manifestPath);
        foreach (var line in contents.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                continue;

            return trimmed["name:".Length..].Trim();
        }

        var directory = Path.GetFileName(Path.GetDirectoryName(manifestPath));
        return string.IsNullOrWhiteSpace(directory) ? "plugin" : directory;
    }

    private static string NormalizePackageId(string packageId)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        var value = packageId.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));

        return value;
    }
}
