namespace Kiln.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Validates plugin directory names so that a name can never escape the <c>plugins</c> directory.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on the interfaces in Kiln.Abstractions and Kiln.Services instead.
/// </remarks>
public static partial class PluginNames
{
    private const int MaxSafeDirectoryNameLength = 128;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> is 1-64 characters of lowercase letters, digits, '.', '_' or '-',
    /// starts with a letter or digit, and therefore contains no path separator or '..' segment.
    /// </summary>
    public static bool IsValid(string? name)
        => name is not null && ValidNamePattern().IsMatch(name);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> is a single, structurally safe directory name: not blank, at most 128 characters,
    /// not '.' or '..', and free of path separators, control characters and characters invalid in file names.
    /// Less strict than <see cref="IsValid"/> so that legacy directories can still be removed.
    /// </summary>
    public static bool IsSafeDirectoryName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxSafeDirectoryNameLength)
            return false;

        if (name is "." or "..")
            return false;

        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in name)
        {
            if (c is '/' or '\\' || char.IsControl(c) || Array.IndexOf(invalidChars, c) >= 0)
                return false;
        }

        return true;
    }

    [GeneratedRegex(@"\A[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ValidNamePattern();
}
