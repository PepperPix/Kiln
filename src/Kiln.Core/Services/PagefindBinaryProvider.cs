namespace Kiln.Services;

using System.ComponentModel;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

public sealed partial class PagefindBinaryProvider : IPagefindBinaryProvider
{
    public const string Version = "1.5.2";
    private const string DownloadBase = "https://github.com/Pagefind/pagefind/releases/download";
    private const string MinimumVersion = "1.5.0";
    private static readonly Version MinimumParsedVersion = System.Version.Parse(MinimumVersion);
    private static readonly TimeSpan VersionCheckTimeout = TimeSpan.FromSeconds(10);

    private readonly string _cacheBasePath;
    private readonly HttpMessageHandler? _httpMessageHandler;
    private readonly string? _pathOverride;
    private readonly IProcessRunner? _processRunner;

    public PagefindBinaryProvider()
        : this(
            Environment.GetEnvironmentVariable("KILN_PAGEFIND_CACHE_DIR")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    // Used by dependency injection: binaries found on PATH are only used when they report at least MinimumVersion.
    public PagefindBinaryProvider(IProcessRunner processRunner)
        : this(
            Environment.GetEnvironmentVariable("KILN_PAGEFIND_CACHE_DIR")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            httpMessageHandler: null,
            pathOverride: null,
            processRunner)
    {
    }

    public PagefindBinaryProvider(string cacheBasePath)
        : this(cacheBasePath, httpMessageHandler: null)
    {
    }

    public PagefindBinaryProvider(string cacheBasePath, HttpMessageHandler? httpMessageHandler)
        : this(cacheBasePath, httpMessageHandler, pathOverride: null)
    {
    }

    public PagefindBinaryProvider(string cacheBasePath, HttpMessageHandler? httpMessageHandler, string? pathOverride)
        : this(cacheBasePath, httpMessageHandler, pathOverride, processRunner: null)
    {
    }

    public PagefindBinaryProvider(
        string cacheBasePath,
        HttpMessageHandler? httpMessageHandler,
        string? pathOverride,
        IProcessRunner? processRunner)
    {
        _cacheBasePath = cacheBasePath;
        _httpMessageHandler = httpMessageHandler;
        _pathOverride = pathOverride;
        _processRunner = processRunner;
    }

    [GeneratedRegex(@"\d+\.\d+(\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex VersionNumberRegex();

    public Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, CancellationToken ct)
        => GetBinaryPathAsync(extended, allowDownload, configuredPath: null, ct);

    public async Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, string? configuredPath, CancellationToken ct)
    {
        // 1. Override via environment variable (explicit user choice: always honored, never version-checked)
        var overridePath = Environment.GetEnvironmentVariable("KILN_PAGEFIND_PATH");
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            return overridePath;

        // 2. search.binaryPath (explicit user choice: never version-checked, a missing file is an error and never falls back)
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configuredPath))
                return configuredPath;

            throw new InvalidOperationException($"search.binaryPath '{configuredPath}' does not exist.");
        }

        // 3. Search PATH directories
        var binaryFileName = GetBinaryFileName(extended);
        var pathSearch = await FindInPathAsync(binaryFileName, ct).ConfigureAwait(false);
        if (pathSearch.Path is not null)
            return pathSearch.Path;

        // 4. Check local cache
        var cacheBinaryPath = GetCacheBinaryPath(extended);
        if (File.Exists(cacheBinaryPath))
            return cacheBinaryPath;

        // 5. Download (only when permitted)
        if (!allowDownload)
        {
            var skippedHint = pathSearch.SkippedReason is null ? string.Empty : $"{pathSearch.SkippedReason} ";
            throw new InvalidOperationException(
                "Pagefind binary not found. " +
                skippedHint +
                "Install it via 'npx pagefind', download from " +
                "https://github.com/Pagefind/pagefind/releases, " +
                "or set the KILN_PAGEFIND_PATH environment variable to point to the binary.");
        }

        await DownloadToCacheAsync(cacheBinaryPath, extended, ct).ConfigureAwait(false);
        return cacheBinaryPath;
    }

    public string GetCacheBinaryPath(bool extended)
    {
        var binaryFileName = GetBinaryFileName(extended);
        return Path.Combine(_cacheBasePath, ".kiln", "tools", "pagefind", Version, binaryFileName);
    }

    private static string GetBinaryFileName(bool extended)
    {
        var baseName = extended ? "pagefind_extended" : "pagefind";
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"{baseName}.exe"
            : baseName;
    }

    private async Task<PathSearchResult> FindInPathAsync(string binaryFileName, CancellationToken ct)
    {
        var pathEnv = _pathOverride ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        string? skippedReason = null;

        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(dir, binaryFileName);
            if (!File.Exists(fullPath))
                continue;

            if (_processRunner is null)
                return new PathSearchResult(fullPath, null);

            var version = await TryReadVersionAsync(fullPath, ct).ConfigureAwait(false);
            if (version is not null && version >= MinimumParsedVersion)
                return new PathSearchResult(fullPath, null);

            skippedReason ??= version is null
                ? $"Skipped '{fullPath}': its version could not be determined (Kiln needs Pagefind {MinimumVersion} or newer)."
                : $"Skipped '{fullPath}': version {version} is older than the required {MinimumVersion}.";
        }

        return new PathSearchResult(null, skippedReason);
    }

    // Returns null when the binary does not report a parseable version (not runnable, timeout, unexpected output).
    private async Task<Version?> TryReadVersionAsync(string binaryPath, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(VersionCheckTimeout);

        ProcessRunResult result;
        try
        {
            result = await _processRunner!.RunAsync(binaryPath, "--version", null, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
            return null;
        }

        if (result.ExitCode != 0)
            return null;

        foreach (var output in new[] { result.StdOut, result.StdErr })
        {
            var match = VersionNumberRegex().Match(output);
            if (match.Success && System.Version.TryParse(match.Value, out var parsed))
                return parsed;
        }

        return null;
    }

    private sealed record PathSearchResult(string? Path, string? SkippedReason);

    private static string GetTargetTriple()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported CPU architecture: {RuntimeInformation.ProcessArchitecture}"),
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"{arch}-apple-darwin";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"{arch}-pc-windows-msvc";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"{arch}-unknown-linux-musl";

        throw new PlatformNotSupportedException(
            $"Unsupported OS platform: {RuntimeInformation.OSDescription}");
    }

    private async Task DownloadToCacheAsync(string cacheBinaryPath, bool extended, CancellationToken ct)
    {
        var triple = GetTargetTriple();
        var baseName = extended ? "pagefind_extended" : "pagefind";
        var assetName = $"{baseName}-v{Version}-{triple}.tar.gz";
        var downloadUrl = $"{DownloadBase}/v{Version}/{assetName}";
        var sha256Url = $"{downloadUrl}.sha256";

        using var http = _httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(_httpMessageHandler, disposeHandler: false);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Kiln-SSG/1.0");

        // Fetch checksum first
        // The checksum comes from the same release as the archive: it detects corruption, not a compromised release.
        var sha256Response = (await http.GetStringAsync(new Uri(sha256Url), ct).ConfigureAwait(false)).Trim();
        var expectedHash = sha256Response.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
            .ToUpperInvariant();

        // Fetch tarball
        var tarballBytes = await http.GetByteArrayAsync(new Uri(downloadUrl), ct).ConfigureAwait(false);

        // Verify integrity
        var actualHash = Convert.ToHexString(SHA256.HashData(tarballBytes));
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"SHA256 mismatch for {assetName}: expected {expectedHash}, got {actualHash}");
        }

        // Prepare cache directory
        var cacheDir = Path.GetDirectoryName(cacheBinaryPath)!;
        Directory.CreateDirectory(cacheDir);

        // Extract into a temp file and move it into place, so an aborted download never leaves a partial binary at the cache path.
        var binaryFileName = Path.GetFileName(cacheBinaryPath);
        var tempBinaryPath = Path.Combine(cacheDir, $"{binaryFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using var tarballStream = new MemoryStream(tarballBytes);
            using var gzip = new GZipStream(tarballStream, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);

            TarEntry? entry;
            var found = false;
            while ((entry = await tar.GetNextEntryAsync(cancellationToken: ct).ConfigureAwait(false)) is not null)
            {
                if (string.Equals(Path.GetFileName(entry.Name), binaryFileName, StringComparison.OrdinalIgnoreCase))
                {
                    await entry.ExtractToFileAsync(tempBinaryPath, overwrite: true, ct).ConfigureAwait(false);
                    if (new FileInfo(tempBinaryPath).Length != entry.Length)
                    {
                        throw new InvalidOperationException(
                            $"Binary '{binaryFileName}' in archive {assetName} is truncated.");
                    }

                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"Binary '{binaryFileName}' not found in archive {assetName}");
            }

            // Set execute permissions on Unix systems
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(
                    tempBinaryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            File.Move(tempBinaryPath, cacheBinaryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempBinaryPath))
                File.Delete(tempBinaryPath);
        }
    }
}
