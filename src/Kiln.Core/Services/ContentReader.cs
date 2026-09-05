namespace Kiln.Services;

using System.Collections.ObjectModel;
using System.Globalization;
using Kiln.Models;
using YamlDotNet.Serialization;

public sealed class ContentReader(IMarkdownProcessor markdownProcessor, IShortcodeProcessor? shortcodeProcessor = null) : IContentReader
{
    private static readonly IDeserializer RawYamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly HashSet<string> KnownFrontMatterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "title", "date", "draft", "layout", "slug", "description",
        "url", "weight", "extra",
        "image_optimization", "imageOptimization",
        "no_index", "noIndex"
    };

    private readonly IShortcodeProcessor _shortcodeProcessor = shortcodeProcessor ?? new ShortcodeProcessor();

    public IReadOnlyList<ContentItem> ReadCollection(
        ContentGroup collection,
        string projectPath,
        IReadOnlyList<PluginDefinition>? plugins = null,
        Collection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var contentDirectory = Path.IsPathRooted(collection.Directory)
            ? collection.Directory
            : Path.Combine(projectPath, collection.Directory);

        if (!Directory.Exists(contentDirectory))
            return [];

        var items = new List<ContentItem>();
        ReadSection(contentDirectory, contentDirectory, collection, items, plugins, warnings);
        return ApplySort(items, collection.Sort);
    }

    private void ReadSection(string dir, string contentDirectory, ContentGroup collection, List<ContentItem> items, IReadOnlyList<PluginDefinition>? plugins, Collection<string>? warnings)
    {
        var sectionPath = ToSectionPath(contentDirectory, dir);

        var files = Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly);
        var fileResults = new ContentItem?[files.Length];
        Parallel.For(0, files.Length, i =>
        {
            fileResults[i] = ReadFile(files[i], contentDirectory, collection, assetDirectory: null, sectionPath, plugins, warnings);
        });
        foreach (var item in fileResults)
        {
            if (item is not null)
                items.Add(item);
        }

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var indexFile = Path.Combine(subDir, "index.md");
            if (File.Exists(indexFile))
            {
                var item = ReadFile(indexFile, contentDirectory, collection, assetDirectory: subDir, sectionPath, plugins, warnings);
                if (item is not null)
                    items.Add(item);
            }
            else
            {
                ReadSection(subDir, contentDirectory, collection, items, plugins, warnings);
            }
        }
    }

    private static string ToSectionPath(string contentDirectory, string dir)
    {
        var rel = Path.GetRelativePath(contentDirectory, dir);
        if (rel == ".")
            return "";
        return rel.Replace(Path.DirectorySeparatorChar, '/');
    }

    public ContentItem ReadSingleFile(
        string absoluteFilePath,
        ContentGroup owningCollection,
        IReadOnlyList<PluginDefinition>? plugins = null,
        Collection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(absoluteFilePath);
        ArgumentNullException.ThrowIfNull(owningCollection);

        if (!File.Exists(absoluteFilePath))
            throw new FileNotFoundException($"Content file not found: {absoluteFilePath}", absoluteFilePath);

        var contentDirectory = Path.GetDirectoryName(absoluteFilePath)
            ?? throw new InvalidOperationException($"Could not resolve content directory for: {absoluteFilePath}");

        var item = ReadFile(
            absoluteFilePath,
            contentDirectory,
            owningCollection,
            assetDirectory: null,
            sectionPath: string.Empty,
            plugins,
            warnings)
            ?? throw new InvalidOperationException($"Content file '{absoluteFilePath}' is missing valid front matter.");

        return item;
    }

    private ContentItem? ReadFile(
        string filePath,
        string contentDirectory,
        ContentGroup collection,
        string? assetDirectory,
        string sectionPath,
        IReadOnlyList<PluginDefinition>? plugins,
        Collection<string>? warnings)
    {
        var content = File.ReadAllText(filePath);
        var (frontMatter, body, extraFromFrontMatter, rawAll) = ParseFrontMatter(content, collection);

        if (frontMatter is null)
            return null;

        var relativePath = Path.GetRelativePath(contentDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');

        var slug = frontMatter.Slug
            ?? (assetDirectory is not null
                ? Path.GetFileName(assetDirectory)
                : Path.GetFileNameWithoutExtension(filePath));

        var extra = new Dictionary<string, object>(frontMatter.Extra);
        foreach (var (k, v) in extraFromFrontMatter)
            extra.TryAdd(k, v);
        if (!string.IsNullOrEmpty(frontMatter.PermalinkOverride))
            extra["permalink_override"] = frontMatter.PermalinkOverride;

        var taxonomies = new Dictionary<string, object>();
        foreach (var taxName in collection.Taxonomies)
        {
            taxonomies[taxName] = rawAll.TryGetValue(taxName, out var rawValue)
                ? ConvertToStringList(rawValue)
                : new List<string>();
        }

        var effectiveSlugForAssets = string.IsNullOrEmpty(sectionPath) ? slug : $"{sectionPath}/{slug}";
        var assetBasePath = assetDirectory is not null
            ? $"/assets/content/{collection.Name}/{effectiveSlugForAssets}/"
            : null;

        var shortcodeWarnings = warnings ?? new Collection<string>();
        var processedBody = _shortcodeProcessor.Process(body, plugins ?? [], shortcodeWarnings);

        var moreMarkerIndex = processedBody.IndexOf(MoreMarker, StringComparison.Ordinal);
        var htmlBody = moreMarkerIndex >= 0
            ? processedBody.Replace(MoreMarker, "", StringComparison.Ordinal)
            : processedBody;

        string? teaser = frontMatter.Description;
        if (string.IsNullOrEmpty(teaser))
        {
            teaser = moreMarkerIndex >= 0
                ? markdownProcessor.ToPlainText(processedBody[..moreMarkerIndex])
                : TruncateToWords(markdownProcessor.ToPlainText(processedBody), collection.TeaserWords);
        }

        return new ContentItem
        {
            Id = frontMatter.Id,
            Title = frontMatter.Title,
            Date = frontMatter.Date,
            Draft = frontMatter.Draft,
            Slug = slug,
            SectionPath = sectionPath,
            Description = frontMatter.Description,
            Teaser = teaser,
            Layout = frontMatter.Layout,
            Weight = frontMatter.Weight,
            SourcePath = filePath,
            RelativePath = relativePath,
            RawContent = processedBody,
            HtmlContent = markdownProcessor.ToHtml(htmlBody, assetBasePath),
            Url = collection.Url,
            OutputPath = "",
            Collection = collection,
            Extra = extra,
            Taxonomies = taxonomies,
            AssetDirectory = assetDirectory,
            ImageOptimization = frontMatter.ImageOptimization ?? true,
            NoIndex = frontMatter.NoIndex
        };
    }

    private const string MoreMarker = "<!--more-->";

    /// <summary>
    /// Truncates plain text to the first <paramref name="wordCount"/> words, cut at a word
    /// boundary, appending "…" only when the text was actually longer than the limit.
    /// </summary>
    private static string TruncateToWords(string text, int wordCount)
    {
        var normalized = text.Replace('\n', ' ').Replace('\r', ' ');
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= wordCount)
            return string.Join(' ', words);
        return string.Join(' ', words[..wordCount]) + "…";
    }

    private static List<ContentItem> ApplySort(List<ContentItem> items, string sort)
    {
        return sort switch
        {
            "date desc" => [.. items.OrderByDescending(i => i.Date)],
            "date asc" => [.. items.OrderBy(i => i.Date)],
            "title asc" => [.. items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase)],
            "weight asc" => [.. items.OrderBy(i => i.Weight).ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)],
            _ => items
        };
    }

    private static (FrontMatter? frontMatter, string body, Dictionary<string, object> extraFromFrontMatter, Dictionary<string, object> rawAll) ParseFrontMatter(string content, ContentGroup collection)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (null, content, [], []);

        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (null, content, [], []);

        var yamlBlock = content[3..endIndex].Trim();
        var body = content[(endIndex + 3)..].Trim();

        var rawAll = RawYamlDeserializer.Deserialize<Dictionary<string, object>>(yamlBlock);
        var frontMatter = DeserializeFrontMatter(rawAll);

        var excludedKeys = new HashSet<string>(KnownFrontMatterKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var taxName in collection.Taxonomies)
            excludedKeys.Add(taxName);
        var extraFromFrontMatter = rawAll
            .Where(kvp => !excludedKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return (frontMatter, body, extraFromFrontMatter, rawAll);
    }

    private static FrontMatter DeserializeFrontMatter(Dictionary<string, object> rawAll)
    {
        var title = GetString(rawAll, "title")
            ?? throw new InvalidOperationException("Front matter is missing the required 'title' field.");

        return new FrontMatter
        {
            Id = GetString(rawAll, "id"),
            Title = title,
            Date = GetNullableDate(rawAll, "date"),
            Draft = GetBool(rawAll, false, "draft"),
            Layout = GetString(rawAll, "layout"),
            Slug = GetString(rawAll, "slug"),
            Description = GetString(rawAll, "description"),
            PermalinkOverride = GetString(rawAll, "url", "permalink_override", "permalinkOverride"),
            Weight = GetInt(rawAll, "weight", 0),
            NoIndex = GetBool(rawAll, false, "no_index", "noIndex"),
            ImageOptimization = GetNullableBool(rawAll, "image_optimization", "imageOptimization"),
            Extra = []
        };
    }

    private static string? GetString(Dictionary<string, object> rawAll, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!rawAll.TryGetValue(key, out var value))
                continue;

            return value switch
            {
                null => null,
                string s => s,
                _ => value.ToString()
            };
        }

        return null;
    }

    private static bool GetBool(Dictionary<string, object> rawAll, bool defaultValue, params string[] keys)
        => ToBool(GetValue(rawAll, keys), defaultValue);

    private static bool? GetNullableBool(Dictionary<string, object> rawAll, params string[] keys)
    {
        var value = GetValue(rawAll, keys);
        if (value is null)
            return null;

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static int GetInt(Dictionary<string, object> rawAll, string key, int defaultValue)
        => ToInt(GetValue(rawAll, key), defaultValue);

    private static DateTime? GetNullableDate(Dictionary<string, object> rawAll, string key)
    {
        var value = GetValue(rawAll, key);
        if (value is null)
            return null;

        return value switch
        {
            DateTime dateTime => dateTime,
            DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            _ => DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                ? parsed
                : null
        };
    }

    private static object? GetValue(Dictionary<string, object> rawAll, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (rawAll.TryGetValue(key, out var value))
                return value;
        }

        return null;
    }

    private static bool ToBool(object? value, bool defaultValue)
    {
        if (value is null)
            return defaultValue;

        if (value is bool boolValue)
            return boolValue;

        return bool.TryParse(value.ToString(), out var parsed)
            ? parsed
            : defaultValue;
    }

    private static int ToInt(object? value, int defaultValue)
    {
        if (value is null)
            return defaultValue;

        if (value is int intValue)
            return intValue;

        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    /// <summary>
    /// Converts a raw YAML value (as produced by <see cref="RawYamlDeserializer"/> when deserializing
    /// into a <see cref="Dictionary{TKey, TValue}"/>) into a string list. Handles a YAML sequence
    /// (deserialized as <see cref="List{Object}"/>), a single bare scalar (e.g. <c>tags: dotnet</c>),
    /// and a missing/null value.
    /// </summary>
    private static List<string> ConvertToStringList(object? rawValue)
    {
        switch (rawValue)
        {
            case null:
                return [];
            case List<object?> list:
                return [.. list.Where(static v => v is not null).Select(static v => v!.ToString()!)];
            default:
                return [rawValue.ToString()!];
        }
    }
}
