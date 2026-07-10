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
        var html = Markdown.ToHtml(EncodeSpacesInLinkDestinations(markdown), _pipeline);
        if (assetBasePath is not null)
            html = RewriteRelativeImageUrls(html, assetBasePath);
        return html;
    }

    public string ToPlainText(string markdown)
    {
        return Markdown.ToPlainText(markdown, _pipeline).Trim();
    }

    // Matches `](destination)` for both links and images, where destination is NOT already
    // angle-bracket-delimited (`](<...>)`, which already permits raw spaces per CommonMark).
    [GeneratedRegex(@"\]\((?!<)([^)]+)\)", RegexOptions.Singleline)]
    private static partial Regex LinkOrImageDestinationRegex();

    // Matches a well-formed trailing CommonMark link title (`"..."` or `'...'`) preceded by
    // whitespace, so its spaces are left untouched while the URL portion in front of it gets encoded.
    [GeneratedRegex("""\s+("[^"]*"|'[^']*')$""")]
    private static partial Regex TrailingTitleRegex();

    /// <summary>
    /// CommonMark (and therefore Markdig) does not allow raw, un-encoded spaces in a bare link/image
    /// destination — a destination containing a space fails to parse entirely, and the whole
    /// <c>![alt](path)</c>/<c>[text](path)</c> construct is left as literal text instead of becoming
    /// an <c>&lt;img&gt;</c>/<c>&lt;a&gt;</c>. Real-world asset file names (uploaded via Kiln Studio's
    /// Asset Library before filename slugification was added, or added by hand outside Studio) can
    /// easily contain spaces, so pre-encode spaces in destinations here rather than requiring every
    /// piece of content to avoid them. Only the URL portion is touched — an optional trailing
    /// CommonMark title (`"..."`/`'...'`) is left untouched since its spaces are meaningful syntax,
    /// not part of the path. Destinations already wrapped in `&lt;...&gt;` are left alone (CommonMark
    /// already permits raw spaces there).
    /// </summary>
    private static string EncodeSpacesInLinkDestinations(string markdown)
    {
        return LinkOrImageDestinationRegex().Replace(markdown, m =>
        {
            var destination = m.Groups[1].Value;
            if (!destination.Contains(' ', StringComparison.Ordinal))
                return m.Value;

            var titleMatch = TrailingTitleRegex().Match(destination);
            var url = titleMatch.Success ? destination[..titleMatch.Index] : destination;
            var titleSuffix = titleMatch.Success ? destination[titleMatch.Index..] : "";

            return $"]({url.Replace(" ", "%20", StringComparison.Ordinal)}{titleSuffix})";
        });
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
