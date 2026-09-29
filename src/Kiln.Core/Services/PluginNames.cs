namespace Kiln.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Validates plugin directory names so that a name can never escape the <c>plugins</c> directory.
/// </summary>
public static partial class PluginNames
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> is 1-64 characters of lowercase letters, digits, '.', '_' or '-',
    /// starts with a letter or digit, and therefore contains no path separator or '..' segment.
    /// </summary>
    public static bool IsValid(string? name)
        => name is not null && ValidNamePattern().IsMatch(name);

    [GeneratedRegex(@"\A[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ValidNamePattern();
}
