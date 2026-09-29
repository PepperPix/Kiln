namespace Kiln.Services;

/// <summary>
/// Project-local, content-addressed cache for optimized image bytes at
/// <c>&lt;project&gt;/.kiln/image-cache/</c>. Keyed by a hash of the source bytes plus the
/// optimization settings, so changing <c>images.*</c> config or the source image itself
/// naturally invalidates the cache entry (no explicit garbage collection in phase 1 — see
/// PLAN-068 §11).
/// </summary>
internal static class ImageOptimizationCache
{
    private const int CacheKeyLength = 16;

    public static (byte[] Bytes, string Extension) GetOrOptimize(
        string projectPath,
        string sourceFile,
        ImageOptimizationSettings settings,
        IImageOptimizer optimizer)
    {
        var sourceBytes = File.ReadAllBytes(sourceFile);
        var ext = Path.GetExtension(sourceFile);
        var cacheDir = Path.Combine(projectPath, ".kiln", "image-cache");
        var key = ComputeCacheKey(sourceBytes, settings);

        var existingCacheFile = Directory.Exists(cacheDir)
            ? Directory.EnumerateFiles(cacheDir, $"{key}.*").FirstOrDefault()
            : null;
        if (existingCacheFile is not null)
            return (File.ReadAllBytes(existingCacheFile), Path.GetExtension(existingCacheFile));

        var result = optimizer.Optimize(sourceBytes, ext, settings);

        Directory.CreateDirectory(cacheDir);
        File.WriteAllBytes(Path.Combine(cacheDir, $"{key}{result.Extension}"), result.Bytes);

        return (result.Bytes, result.Extension);
    }

    private static string ComputeCacheKey(byte[] sourceBytes, ImageOptimizationSettings settings)
    {
        using var sha256 = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        sha256.AppendData(sourceBytes);
        sha256.AppendData(System.Text.Encoding.UTF8.GetBytes($"{settings.MaxWidth}|{settings.Quality}|{settings.ConvertToWebp}"));
        return Convert.ToHexStringLower(sha256.GetHashAndReset())[..CacheKeyLength];
    }
}
