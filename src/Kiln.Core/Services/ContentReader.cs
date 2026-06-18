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

    public IReadOnlyList<ContentItem> ReadCollection(ContentGroup collection, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var contentDirectory = Path.IsPathRooted(collection.Directory)
            ? collection.Directory
            : Path.Combine(projectPath, collection.Directory);

        if (!Directory.Exists(contentDirectory))
            return [];

        var files = Directory.GetFiles(contentDirectory, "*.md", SearchOption.AllDirectories);
        var items = new List<ContentItem>(files.Length);

        foreach (var file in files)
        {
            var item = ReadFile(file, contentDirectory, collection);
            if (item is not null)
                items.Add(item);
        }

        return ApplySort(items, collection.Sort);
    }

    private ContentItem? ReadFile(string filePath, string contentDirectory, ContentGroup collection)
    {
        var content = File.ReadAllText(filePath);
        var (frontMatter, body) = ParseFrontMatter(content);

        if (frontMatter is null)
            return null;

        var relativePath = Path.GetRelativePath(contentDirectory, filePath);
        var slug = frontMatter.Slug ?? Path.GetFileNameWithoutExtension(filePath);

        var extra = new Dictionary<string, object>(frontMatter.Extra);
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
            HtmlContent = markdownProcessor.ToHtml(body),
            Url = collection.Url,
            OutputPath = "",
            Collection = collection,
            Extra = extra,
            Taxonomies = taxonomies
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

    private static (FrontMatter? frontMatter, string body) ParseFrontMatter(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (null, content);

        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (null, content);

        var yamlBlock = content[3..endIndex].Trim();
        var body = content[(endIndex + 3)..].Trim();

        var frontMatter = YamlDeserializer.Deserialize<FrontMatter>(yamlBlock);
        return (frontMatter, body);
    }
}

