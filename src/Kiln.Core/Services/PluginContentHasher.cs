namespace Kiln.Services;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Computes a deterministic content hash over an installed plugin directory.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on the interfaces in Kiln.Abstractions and Kiln.Services instead.
/// </remarks>
public static class PluginContentHasher
{
    /// <summary>
    /// Returns the lowercase hex SHA-256 over all files of <paramref name="directory"/>, ordered by relative path
    /// (ordinal, '/' separators), each contributing its relative path, a NUL byte and its own SHA-256.
    /// </summary>
    public static string ComputeDirectoryHash(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = Path.GetFullPath(directory);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => (Path: Path.GetRelativePath(root, file).Replace('\\', '/'), Full: file))
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToList();

        using var total = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (relativePath, fullPath) in files)
        {
            total.AppendData(Encoding.UTF8.GetBytes(relativePath));
            total.AppendData([0]);
            using var stream = File.OpenRead(fullPath);
            total.AppendData(SHA256.HashData(stream));
        }

        return Convert.ToHexStringLower(total.GetHashAndReset());
    }
}
