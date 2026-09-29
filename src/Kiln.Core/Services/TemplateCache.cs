namespace Kiln.Services;

using System.Collections.Concurrent;
using Scriban;

/// <summary>
/// Caches parsed Scriban templates per file so layouts, partials, slots and shortcodes are parsed once per change.
/// </summary>
internal static class TemplateCache
{
    private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the parsed template for <paramref name="path"/>; re-parses when the file's last-write time or length changed.
    /// Templates with parse errors are returned but never cached.
    /// </summary>
    public static Template GetOrParse(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);

        if (info.Exists
            && Entries.TryGetValue(fullPath, out var cached)
            && cached.LastWriteTimeUtc == info.LastWriteTimeUtc
            && cached.Length == info.Length)
        {
            return cached.Template;
        }

        // Stamp is read before the content: a concurrent edit only causes one extra re-parse later.
        var lastWriteTimeUtc = info.LastWriteTimeUtc;
        var length = info.Exists ? info.Length : 0;

        var template = Template.Parse(File.ReadAllText(fullPath), path);
        if (template.HasErrors)
        {
            Entries.TryRemove(fullPath, out _);
            return template;
        }

        Entries[fullPath] = new CacheEntry(lastWriteTimeUtc, length, template);
        return template;
    }

    private sealed record CacheEntry(DateTime LastWriteTimeUtc, long Length, Template Template);
}
