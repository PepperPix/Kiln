namespace Kiln.Services;

using System.IO.Compression;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class NuGetPluginClient : INuGetPluginClient
{
    private const int MaxContentEntries = 2000;
    private const long MaxEntryBytes = 10L * 1024 * 1024;
    private const long MaxTotalBytes = 50L * 1024 * 1024;
    private const string DefaultServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private const string ContentPrefix = "content/";
    private const int CopyBufferSize = 81920;
    private static readonly IDeserializer ManifestDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
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
            var contentEntries = archive.Entries
                .Where(e => e.FullName.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (contentEntries.Count > MaxContentEntries)
                throw new InvalidOperationException($"Package '{normalizedPackageId}' contains more than {MaxContentEntries} content entries.");

            var destinationRoot = PathContainment.Normalize(contentRoot);
            long totalBytes = 0;
            foreach (var entry in contentEntries)
            {
                var relativePath = entry.FullName[ContentPrefix.Length..];
                if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Equals(".", StringComparison.Ordinal) || relativePath.EndsWith('/'))
                    continue;

                if (Path.IsPathRooted(relativePath) || relativePath.Split(['/', '\\']).Contains(".."))
                    throw new InvalidOperationException($"Archive entry '{entry.FullName}' is outside the plugin content directory.");

                var destinationPath = Path.GetFullPath(Path.Combine(contentRoot, relativePath));
                if (!PathContainment.IsDescendant(destinationRoot, destinationPath))
                    throw new InvalidOperationException($"Archive entry '{entry.FullName}' is outside the plugin content directory.");

                if (entry.Length > MaxEntryBytes)
                    throw new InvalidOperationException($"Archive entry '{entry.FullName}' exceeds the maximum size of {MaxEntryBytes} bytes.");

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                totalBytes += await CopyEntryAsync(entry, destinationPath, MaxTotalBytes - totalBytes, ct).ConfigureAwait(false);
            }

            var pluginManifestPath = FindManifestPath(contentRoot);
            if (pluginManifestPath is null)
                throw new InvalidOperationException($"Package '{normalizedPackageId}' does not contain a plugin.yaml manifest.");

            var pluginName = ReadPluginName(pluginManifestPath, normalizedPackageId);
            var projectRoot = Path.GetFullPath(projectPath);
            var pluginsRoot = PathContainment.Normalize(Path.Combine(projectRoot, "plugins"));
            var destinationDir = PathContainment.Normalize(Path.Combine(pluginsRoot, pluginName));
            if (!PathContainment.AreSame(pluginsRoot, Path.GetDirectoryName(destinationDir) ?? string.Empty))
                throw new InvalidOperationException($"Plugin name '{pluginName}' from package '{normalizedPackageId}' is not a valid plugin directory name.");

            if (Directory.Exists(destinationDir))
                Directory.Delete(destinationDir, recursive: true);
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

    private static async Task<long> CopyEntryAsync(ZipArchiveEntry entry, string destinationPath, long remainingTotalBytes, CancellationToken ct)
    {
        var limit = Math.Min(MaxEntryBytes, remainingTotalBytes);
        long written = 0;
        var buffer = new byte[CopyBufferSize];

        var source = await entry.OpenAsync(ct).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            var target = CreateExtractionStream(destinationPath);
            await using (target.ConfigureAwait(false))
            {
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    written += read;
                    if (written > limit)
                        throw new InvalidOperationException($"Archive entry '{entry.FullName}' exceeds the plugin package size limits.");

                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                }
            }
        }

        return written;
    }

    // The caller has verified that destinationPath lies inside the extraction root (no '..' segments, not rooted).
#pragma warning disable CA5389 // Zip-slip: path sanitized by the caller before this stream is created
    private static FileStream CreateExtractionStream(string destinationPath)
        => new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true);
#pragma warning restore CA5389

    private static string ReadPluginName(string manifestPath, string packageId)
    {
        string? name;
        try
        {
            var manifest = ManifestDeserializer.Deserialize<Dictionary<string, object?>?>(File.ReadAllText(manifestPath));
            name = manifest is not null && manifest.TryGetValue("name", out var rawName)
                ? Convert.ToString(rawName, System.Globalization.CultureInfo.InvariantCulture)?.Trim()
                : null;
        }
        catch (YamlException ex)
        {
            throw new InvalidOperationException($"The plugin manifest of package '{packageId}' is not valid YAML: {ex.Message}", ex);
        }

        if (string.IsNullOrEmpty(name))
            name = packageId.Split('.')[^1];

        if (!PluginNames.IsValid(name))
            throw new InvalidOperationException($"Plugin name '{name}' from package '{packageId}' is not a valid plugin directory name.");

        return name;
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
