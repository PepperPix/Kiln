namespace Kiln.Services;

using Kiln.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class ContentReader(IMarkdownProcessor markdownProcessor) : IContentReader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly IDeserializer RawYamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly HashSet<string> KnownFrontMatterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "title", "date", "draft", "layout", "slug", "description",
        "url", "weight", "extra"
    };

    public IReadOnlyList<ContentItem> ReadCollection(ContentGroup collection, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var contentDirectory = Path.IsPathRooted(collection.Directory)
            ? collection.Directory
            : Path.Combine(projectPath, collection.Directory);

        if (!Directory.Exists(contentDirectory))
            return [];

        var items = new List<ContentItem>();
        ReadSection(contentDirectory, contentDirectory, collection, items);
        return ApplySort(items, collection.Sort);
    }

    private void ReadSection(string dir, string contentDirectory, ContentGroup collection, List<ContentItem> items)
    {
        var sectionPath = ToSectionPath(contentDirectory, dir);

        foreach (var file in Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly))
        {
            var item = ReadFile(file, contentDirectory, collection, assetDirectory: null, sectionPath);
            if (item is not null)
                items.Add(item);
        }

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var indexFile = Path.Combine(subDir, "index.md");
            if (File.Exists(indexFile))
            {
                var item = ReadFile(indexFile, contentDirectory, collection, assetDirectory: subDir, sectionPath);
                if (item is not null)
                    items.Add(item);
            }
            else
            {
                ReadSection(subDir, contentDirectory, collection, items);
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

    public ContentItem ReadSingleFile(string absoluteFilePath, ContentGroup owningCollection)
    {
        ArgumentNullException.ThrowIfNull(absoluteFilePath);
        ArgumentNullException.ThrowIfNull(owningCollection);

        if (!File.Exists(absoluteFilePath))
            throw new FileNotFoundException($"Content file not found: {absoluteFilePath}", absoluteFilePath);

        var contentDirectory = Path.GetDirectoryName(absoluteFilePath)
            ?? throw new InvalidOperationException($"Could not resolve content directory for: {absoluteFilePath}");

        var item = ReadFile(absoluteFilePath, contentDirectory, owningCollection, assetDirectory: null, sectionPath: "")
            ?? throw new InvalidOperationException($"Content file '{absoluteFilePath}' is missing valid front matter.");

        return item;
    }

    private ContentItem? ReadFile(string filePath, string contentDirectory, ContentGroup collection, string? assetDirectory, string sectionPath)
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

        return new ContentItem
        {
            Id = frontMatter.Id,
            Title = frontMatter.Title,
            Date = frontMatter.Date,
            Draft = frontMatter.Draft,
            Slug = slug,
            SectionPath = sectionPath,
            Description = frontMatter.Description,
            Layout = frontMatter.Layout,
            Weight = frontMatter.Weight,
            SourcePath = filePath,
            RelativePath = relativePath,
            RawContent = body,
            HtmlContent = markdownProcessor.ToHtml(body, assetBasePath),
            Url = collection.Url,
            OutputPath = "",
            Collection = collection,
            Extra = extra,
            Taxonomies = taxonomies,
            AssetDirectory = assetDirectory
        };
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

        var frontMatter = YamlDeserializer.Deserialize<FrontMatter>(yamlBlock);

        var rawAll = RawYamlDeserializer.Deserialize<Dictionary<string, object>>(yamlBlock);
        var excludedKeys = new HashSet<string>(KnownFrontMatterKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var taxName in collection.Taxonomies)
            excludedKeys.Add(taxName);
        var extraFromFrontMatter = rawAll
            .Where(kvp => !excludedKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return (frontMatter, body, extraFromFrontMatter, rawAll);
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
