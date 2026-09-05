namespace Kiln.Services;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class NuGetPluginClient : INuGetPluginClient
{
    private const string DefaultBaseUrlText = "https://api.nuget.org/v3";

    private readonly Uri _baseUri;
    private readonly HttpClient _httpClient;

    public NuGetPluginClient()
        : this(new Uri(DefaultBaseUrlText), httpMessageHandler: null)
    {
    }

    public NuGetPluginClient(string baseUrl)
        : this(new Uri(baseUrl), httpMessageHandler: null)
    {
    }

    public NuGetPluginClient(string baseUrl, HttpMessageHandler? httpMessageHandler)
        : this(new Uri(baseUrl), httpMessageHandler)
    {
    }

    public NuGetPluginClient(Uri baseUri, HttpMessageHandler? httpMessageHandler)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        _baseUri = new Uri(baseUri.ToString().TrimEnd('/') + "/");
        _httpClient = httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(httpMessageHandler, disposeHandler: false);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Kiln/1.0");
    }

    public async Task<IReadOnlyList<PluginSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var terms = ToSearchTerms(query);
        var url = new Uri(_baseUri, $"query?q={Uri.EscapeDataString(terms)}&prerelease=false&semVerLevel=2");
        var dto = await GetJsonAsync<NuGetSearchResponse>(url, ct).ConfigureAwait(false);

        var results = new List<PluginSearchResult>();
        foreach (var item in dto.Data)
        {
            if (!item.Tags.Contains("kiln-plugin", StringComparer.OrdinalIgnoreCase))
                continue;

            results.Add(new PluginSearchResult(item.Id, item.Version, item.Description ?? string.Empty));
        }

        return results;
    }

    public Task<string?> GetLatestVersionAsync(string packageId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        return GetLatestVersionCoreAsync(NormalizePackageId(packageId), ct);
    }

    private async Task<string?> GetLatestVersionCoreAsync(string normalizedPackageId, CancellationToken ct)
    {
        var url = new Uri(_baseUri, $"registration5-semver2/{normalizedPackageId}/index.json");
        var dto = await GetJsonAsync<NuGetRegistrationResponse>(url, ct).ConfigureAwait(false);

        var versions = new List<string>();
        foreach (var page in dto.Items)
        {
            foreach (var item in page.Items)
                versions.Add(item.CatalogEntry.Version);
        }

        if (versions.Count == 0)
            return null;

        return versions
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .MaxBy(v => ParseVersion(v));
    }

    public Task<bool> IsUpdateAvailableAsync(string packageId, string currentVersion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(currentVersion);

        return IsUpdateAvailableCoreAsync(packageId, currentVersion, ct);
    }

    private async Task<bool> IsUpdateAvailableCoreAsync(string packageId, string currentVersion, CancellationToken ct)
    {
        var latestVersion = await GetLatestVersionAsync(packageId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(latestVersion))
            return false;

        return CompareVersions(latestVersion, currentVersion) > 0;
    }

    public Task<PluginPackageInstallResult> AddAsync(string packageId, string? version, string projectPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(projectPath);

        return AddCoreAsync(packageId, version, projectPath, ct);
    }

    private async Task<PluginPackageInstallResult> AddCoreAsync(string packageId, string? version, string projectPath, CancellationToken ct)
    {
        var normalizedPackageId = NormalizePackageId(packageId);
        var resolvedVersion = string.IsNullOrWhiteSpace(version)
            ? await GetLatestVersionAsync(normalizedPackageId, ct).ConfigureAwait(false)
            : version;

        if (string.IsNullOrWhiteSpace(resolvedVersion))
            throw new InvalidOperationException($"No {nameof(version)} found for package '{packageId}'.");

        var registrationUrl = new Uri(_baseUri, $"registration5-semver2/{normalizedPackageId}/index.json");
        var registration = await GetJsonAsync<NuGetRegistrationResponse>(registrationUrl, ct).ConfigureAwait(false);
        var catalogEntry = FindCatalogEntry(registration, resolvedVersion) ??
            throw new InvalidOperationException($"Package '{packageId}' version '{resolvedVersion}' was not found in the NuGet registration feed.");

        var packageHash = catalogEntry.PackageHash;
        var packageHashAlgorithm = catalogEntry.PackageHashAlgorithm;
        if (!string.Equals(packageHashAlgorithm, "SHA512", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported package hash algorithm '{packageHashAlgorithm}' for '{packageId}'. Only SHA512 is supported.");

        var packageUrlText = catalogEntry.PackageContent;
        if (string.IsNullOrWhiteSpace(packageUrlText))
            throw new InvalidOperationException($"Package '{packageId}' is missing packageContent metadata.");

        using var packageResponse = await _httpClient.GetAsync(new Uri(packageUrlText), ct).ConfigureAwait(false);
        if (!packageResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"NuGet package request failed for '{packageId}' with status {(int)packageResponse.StatusCode} {packageResponse.StatusCode}.");

        var packageBytes = await packageResponse.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var actualHash = Convert.ToHexString(SHA512.HashData(packageBytes));
        if (!string.Equals(actualHash, packageHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SHA512 mismatch for '{packageId}' {nameof(version)} '{resolvedVersion}': expected {packageHash}, got {actualHash}");
        }

        using var archive = new ZipArchive(new MemoryStream(packageBytes), ZipArchiveMode.Read, leaveOpen: false);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-plugin-{Guid.NewGuid():N}");
        var contentRoot = Path.Combine(tempRoot, "content");
        Directory.CreateDirectory(contentRoot);

        try
        {
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("content/", StringComparison.OrdinalIgnoreCase)))
            {
                var relativePath = entry.FullName["content/".Length..];
                if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Equals(".", StringComparison.Ordinal))
                    continue;

                var destinationPath = Path.GetFullPath(Path.Combine(contentRoot, relativePath));
                var contentRootFull = Path.GetFullPath(contentRoot);
                if (!destinationPath.StartsWith(contentRootFull, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Archive entry '{entry.FullName}' is outside the plugin content directory.");

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                await entry.ExtractToFileAsync(destinationPath, overwrite: true, ct).ConfigureAwait(false);
            }

            var pluginManifestPath = Path.Combine(contentRoot, "plugin.yaml");
            if (!File.Exists(pluginManifestPath))
                pluginManifestPath = Path.Combine(contentRoot, "plugin.yml");
            if (!File.Exists(pluginManifestPath))
                throw new InvalidOperationException($"Package '{packageId}' does not contain a plugin.yaml manifest.");

            var pluginName = ReadPluginName(pluginManifestPath);
            var projectRoot = Path.GetFullPath(projectPath);
            var destinationDir = Path.Combine(projectRoot, "plugins", pluginName);
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.EnumerateFiles(contentRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(contentRoot, file);
                var targetPath = Path.Combine(destinationDir, relativePath);
                var targetDir = Path.GetDirectoryName(targetPath)!;
                Directory.CreateDirectory(targetDir);
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

    private static string ToSearchTerms(string query)
        => string.IsNullOrWhiteSpace(query) ? "kiln-plugin" : $"{query} tags:kiln-plugin";

    private static string NormalizePackageId(string packageId)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        var value = packageId.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Package ID cannot be empty.", nameof(packageId));

        return value;
    }

    private static NuGetCatalogEntry? FindCatalogEntry(NuGetRegistrationResponse dto, string version)
    {
        foreach (var page in dto.Items)
        {
            foreach (var item in page.Items)
            {
                if (string.Equals(item.CatalogEntry.Version, version, StringComparison.OrdinalIgnoreCase))
                    return item.CatalogEntry;
            }
        }

        return null;
    }

    private static string ReadPluginName(string manifestPath)
    {
        var raw = File.ReadAllText(manifestPath);
        foreach (var line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                return trimmed["name:".Length..].Trim();
        }

        var directory = Path.GetFileName(Path.GetDirectoryName(manifestPath));
        return string.IsNullOrWhiteSpace(directory) ? "plugin" : directory;
    }

    private static int CompareVersions(string left, string right)
    {
        var leftVersion = ParseVersion(left);
        var rightVersion = ParseVersion(right);

        var comparison = leftVersion.Major.CompareTo(rightVersion.Major);
        if (comparison != 0)
            return comparison;

        comparison = leftVersion.Minor.CompareTo(rightVersion.Minor);
        if (comparison != 0)
            return comparison;

        comparison = leftVersion.Patch.CompareTo(rightVersion.Patch);
        if (comparison != 0)
            return comparison;

        return string.Compare(leftVersion.PreRelease, rightVersion.PreRelease, StringComparison.Ordinal);
    }

    private static VersionParts ParseVersion(string input)
    {
        var value = input.Trim();
        var baseSegment = value.Split('+', 2)[0];
        var prerelease = string.Empty;
        var versionPart = baseSegment;

        var dashIndex = baseSegment.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex >= 0)
        {
            versionPart = baseSegment[..dashIndex];
            prerelease = baseSegment[(dashIndex + 1)..];
        }

        var numbers = versionPart.TrimStart('v').Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var major = ParseInt(numbers, 0, 0);
        var minor = ParseInt(numbers, 1, 0);
        var patch = ParseInt(numbers, 2, 0);
        return new VersionParts(major, minor, patch, prerelease);
    }

    private static int ParseInt(string[] numbers, int index, int fallback)
    {
        if (index >= numbers.Length)
            return fallback;

        if (!int.TryParse(numbers[index], out var value))
            return fallback;

        return value;
    }

    private async Task<T> GetJsonAsync<T>(Uri url, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"NuGet request failed for '{url}' with status {(int)response.StatusCode} {response.StatusCode}.");

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"NuGet response for '{url}' was empty.");
    }

    private sealed record VersionParts(int Major, int Minor, int Patch, string PreRelease);

    private sealed class NuGetSearchResponse
    {
        [JsonPropertyName("data")] public List<NuGetSearchItem> Data { get; set; } = [];
    }

    private sealed class NuGetSearchItem
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    }

    private sealed class NuGetRegistrationResponse
    {
        [JsonPropertyName("items")] public List<NuGetRegistrationPage> Items { get; set; } = [];
    }

    private sealed class NuGetRegistrationPage
    {
        [JsonPropertyName("items")] public List<NuGetRegistrationPackage> Items { get; set; } = [];
    }

    private sealed class NuGetRegistrationPackage
    {
        [JsonPropertyName("catalogEntry")] public NuGetCatalogEntry CatalogEntry { get; set; } = new();
    }

    private sealed class NuGetCatalogEntry
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
        [JsonPropertyName("packageHash")] public string PackageHash { get; set; } = string.Empty;
        [JsonPropertyName("packageHashAlgorithm")] public string PackageHashAlgorithm { get; set; } = string.Empty;
        [JsonPropertyName("packageContent")] public string PackageContent { get; set; } = string.Empty;
    }
}
