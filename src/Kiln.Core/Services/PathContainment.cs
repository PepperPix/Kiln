namespace Kiln.Services;

/// <summary>
/// Separator-aware path containment checks shared by the dev server, output directory validation and plugin installation.
/// </summary>
internal static class PathContainment
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>Returns the absolute path without a trailing directory separator.</summary>
    internal static string Normalize(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    internal static bool AreSame(string normalizedA, string normalizedB)
        => string.Equals(normalizedA, normalizedB, PathComparison);

    /// <summary>True when <paramref name="normalizedCandidate"/> lies strictly below <paramref name="normalizedRoot"/>.</summary>
    internal static bool IsDescendant(string normalizedRoot, string normalizedCandidate)
    {
        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(prefix, PathComparison);
    }

    /// <summary>True when <paramref name="normalizedCandidate"/> equals <paramref name="normalizedRoot"/> or lies below it.</summary>
    internal static bool IsSameOrDescendant(string normalizedRoot, string normalizedCandidate)
        => AreSame(normalizedRoot, normalizedCandidate) || IsDescendant(normalizedRoot, normalizedCandidate);
}
