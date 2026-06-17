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

    public IReadOnlyList<ContentItem> ReadAll(string contentDirectory, string outputDir)
    {
        if (!Directory.Exists(contentDirectory))
            return [];

        var files = Directory.GetFiles(contentDirectory, "*.md", SearchOption.AllDirectories);
        var items = new List<ContentItem>(files.Length);

        foreach (var file in files)
        {
            var item = ReadFile(file, contentDirectory, outputDir);
            if (item is not null)
                items.Add(item);
        }

        return items.OrderByDescending(i => i.FrontMatter.Date).ToList();
    }

    private ContentItem? ReadFile(string filePath, string contentDirectory, string outputDir)
    {
        var content = File.ReadAllText(filePath);
        var (frontMatter, body) = ParseFrontMatter(content);

        if (frontMatter is null)
            return null;

        var relativePath = Path.GetRelativePath(contentDirectory, filePath);
        var slug = frontMatter.Slug ?? Path.GetFileNameWithoutExtension(filePath);
        var outputPath = Path.Combine(slug, "index.html");

        return new ContentItem
        {
            SourcePath = filePath,
            RelativePath = relativePath,
            FrontMatter = frontMatter,
            RawContent = body,
            HtmlContent = markdownProcessor.ToHtml(body),
            OutputPath = outputPath
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
