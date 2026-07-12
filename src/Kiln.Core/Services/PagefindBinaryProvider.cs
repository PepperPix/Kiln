namespace Kiln.Services;

using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

public sealed class PagefindBinaryProvider : IPagefindBinaryProvider
{
    public const string Version = "1.5.2";
    private const string DownloadBase = "https://github.com/Pagefind/pagefind/releases/download";

    private readonly string _cacheBasePath;
    private readonly HttpMessageHandler? _httpMessageHandler;
    private readonly string? _pathOverride;

    public PagefindBinaryProvider()
        : this(
            Environment.GetEnvironmentVariable("KILN_PAGEFIND_CACHE_DIR")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
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
    {
        _cacheBasePath = cacheBasePath;
        _httpMessageHandler = httpMessageHandler;
        _pathOverride = pathOverride;
    }

    public async Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, CancellationToken ct)
    {
        // 1. Override via environment variable
        var overridePath = Environment.GetEnvironmentVariable("KILN_PAGEFIND_PATH");
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            return overridePath;

        // 2. Search PATH directories
        var binaryFileName = GetBinaryFileName(extended);
        var pathBinary = FindInPath(binaryFileName, _pathOverride);
        if (pathBinary is not null)
            return pathBinary;

        // 3. Check local cache
        var cacheBinaryPath = GetCacheBinaryPath(extended);
        if (File.Exists(cacheBinaryPath))
            return cacheBinaryPath;

        // 4. Download (only when permitted)
        if (!allowDownload)
        {
            throw new InvalidOperationException(
                "Pagefind binary not found. " +
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

    private static string? FindInPath(string binaryFileName, string? pathOverride)
    {
        var pathEnv = pathOverride ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(dir, binaryFileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

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

        // Extract the binary from the tarball
        var binaryFileName = Path.GetFileName(cacheBinaryPath);
        using var tarballStream = new MemoryStream(tarballBytes);
        using var gzip = new GZipStream(tarballStream, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);

        TarEntry? entry;
        var found = false;
        while ((entry = await tar.GetNextEntryAsync(cancellationToken: ct).ConfigureAwait(false)) is not null)
        {
            if (string.Equals(Path.GetFileName(entry.Name), binaryFileName, StringComparison.OrdinalIgnoreCase))
            {
                await entry.ExtractToFileAsync(cacheBinaryPath, overwrite: true, ct).ConfigureAwait(false);
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
                cacheBinaryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }
}
