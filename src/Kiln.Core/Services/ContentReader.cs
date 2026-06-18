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
        "url", "weight", "tags", "categories", "extra"
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

        // Plain .md files at the top level of the collection directory
        foreach (var file in Directory.GetFiles(contentDirectory, "*.md", SearchOption.TopDirectoryOnly))
        {
            var item = ReadFile(file, contentDirectory, collection, assetDirectory: null);
            if (item is not null)
                items.Add(item);
        }

        // Page Bundles: subdirectories that contain index.md
        foreach (var subDir in Directory.GetDirectories(contentDirectory))
        {
            var indexFile = Path.Combine(subDir, "index.md");
            if (!File.Exists(indexFile))
                continue;

            var item = ReadFile(indexFile, contentDirectory, collection, assetDirectory: subDir);
            if (item is not null)
                items.Add(item);
        }

        return ApplySort(items, collection.Sort);
    }

    private ContentItem? ReadFile(string filePath, string contentDirectory, ContentGroup collection, string? assetDirectory)
    {
        var content = File.ReadAllText(filePath);
        var (frontMatter, body, extraFromFrontMatter) = ParseFrontMatter(content);

        if (frontMatter is null)
            return null;

        var relativePath = Path.GetRelativePath(contentDirectory, filePath);

        // For Page Bundles the default slug is the directory name, not "index"
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
            if (string.Equals(taxName, "tags", StringComparison.OrdinalIgnoreCase))
                taxonomies[taxName] = frontMatter.Tags;
            else if (string.Equals(taxName, "categories", StringComparison.OrdinalIgnoreCase))
                taxonomies[taxName] = frontMatter.Categories;
        }

        var assetBasePath = assetDirectory is not null
            ? $"/assets/content/{collection.Name}/{slug}/"
            : null;

        return new ContentItem
        {
            Id = frontMatter.Id,
            Title = frontMatter.Title,
            Date = frontMatter.Date,
            Draft = frontMatter.Draft,
            Slug = slug,
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

    private static (FrontMatter? frontMatter, string body, Dictionary<string, object> extraFromFrontMatter) ParseFrontMatter(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (null, content, []);

        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (null, content, []);

        var yamlBlock = content[3..endIndex].Trim();
        var body = content[(endIndex + 3)..].Trim();

        var frontMatter = YamlDeserializer.Deserialize<FrontMatter>(yamlBlock);

        var rawAll = RawYamlDeserializer.Deserialize<Dictionary<string, object>>(yamlBlock)
            ?? [];
        var extraFromFrontMatter = rawAll
            .Where(kvp => !KnownFrontMatterKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return (frontMatter, body, extraFromFrontMatter);
    }
}

