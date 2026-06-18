namespace Kiln.Services;

using System.Text.RegularExpressions;
using Markdig;

public sealed partial class MarkdownProcessor : IMarkdownProcessor
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownProcessor()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string ToHtml(string markdown, string? assetBasePath = null)
    {
        var html = Markdown.ToHtml(markdown, _pipeline);
        if (assetBasePath is not null)
            html = RewriteRelativeImageUrls(html, assetBasePath);
        return html;
    }

    // Matches <img ... src="<relative-path>"> where the src value is not an absolute URL or root-relative path.
    [GeneratedRegex("""(<img\b[^>]*?\bsrc=")(?!https?://|//)(?!/)([^"]*)(")""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RelativeImgSrcRegex();

    private const char UrlSeparatorChar = '/';
    private const string DotSlashPrefix = "./";

    private static string RewriteRelativeImageUrls(string html, string assetBasePath)
    {
        var basePath = assetBasePath.TrimEnd(UrlSeparatorChar) + UrlSeparatorChar;
        return RelativeImgSrcRegex().Replace(html, m =>
        {
            var openQuote = m.Groups[1].Value;
            var relativeSrc = m.Groups[2].Value;
            var closeQuote = m.Groups[3].Value;
            if (relativeSrc.StartsWith(DotSlashPrefix, StringComparison.Ordinal))
                relativeSrc = relativeSrc[DotSlashPrefix.Length..];
            return openQuote + basePath + relativeSrc + closeQuote;
        });
    }
}
