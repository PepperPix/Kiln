namespace Kiln.Services;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Inventories a plugin directory and lists the external hosts its files refer to.
/// </summary>
public static partial class PluginContentInspector
{
    private const int RegexTimeoutMilliseconds = 2000;
    private static readonly string[] IgnoredHosts = ["www.w3.org"];
    private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase) { ".js", ".mjs" };
    private static readonly HashSet<string> StyleExtensions = new(StringComparer.OrdinalIgnoreCase) { ".css" };
    private static readonly HashSet<string> HtmlExtensions = new(StringComparer.OrdinalIgnoreCase) { ".html", ".htm" };
    private static readonly HashSet<string> ScannedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".mjs", ".css", ".html", ".htm", ".svg", ".json",
    };

    /// <summary>
    /// Scans <paramref name="directory"/>. The host list is a heuristic: it finds <c>http(s)://host</c> and
    /// protocol-relative <c>//host</c> references in text, but cannot see URLs a script builds at runtime.
    /// </summary>
    public static PluginContentScan Scan(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = Path.GetFullPath(directory);
        var files = new List<PluginFileInfo>();
        var hosts = new SortedSet<string>(StringComparer.Ordinal);
        var scripts = 0;
        var styles = 0;
        var html = 0;
        var other = 0;
        long total = 0;

        var paths = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => (Relative: Path.GetRelativePath(root, file).Replace('\\', '/'), Full: file))
            .OrderBy(item => item.Relative, StringComparer.Ordinal);

        foreach (var (relative, full) in paths)
        {
            var size = new FileInfo(full).Length;
            files.Add(new PluginFileInfo(relative, size));
            total += size;

            var extension = Path.GetExtension(relative);
            if (ScriptExtensions.Contains(extension))
                scripts++;
            else if (StyleExtensions.Contains(extension))
                styles++;
            else if (HtmlExtensions.Contains(extension))
                html++;
            else
                other++;

            if (ScannedExtensions.Contains(extension))
                CollectHosts(File.ReadAllText(full), hosts);
        }

        return new PluginContentScan(files, new PluginInventory(scripts, styles, html, other, total), [.. hosts]);
    }

    private static void CollectHosts(string text, SortedSet<string> hosts)
    {
        foreach (Match match in HostPattern().Matches(text))
        {
            var host = CultureInfo.InvariantCulture.TextInfo.ToLower(match.Groups["host"].Value);
            if (!IgnoredHosts.Contains(host, StringComparer.Ordinal))
                hosts.Add(host);
        }
    }

    // Explicit scheme: any host. Protocol-relative: the host must contain a dot, which skips line comments such as "//note".
    [GeneratedRegex(
        @"(?:\bhttps?://(?<host>[A-Za-z0-9](?:[A-Za-z0-9.\-]*[A-Za-z0-9])?)|(?<![\w:/.\-])//(?<host>[A-Za-z0-9][A-Za-z0-9\-]*(?:\.[A-Za-z0-9\-]+)+))(?::\d+)?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        RegexTimeoutMilliseconds)]
    private static partial Regex HostPattern();
}
