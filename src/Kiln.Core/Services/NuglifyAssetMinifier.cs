namespace Kiln.Services;

using System.Text.RegularExpressions;
using Kiln.Abstractions;
using NUglify;
using NUglify.Html;

public sealed partial class NuglifyAssetMinifier : IAssetMinifier
{
    private readonly bool _htmlAggressive;

    public NuglifyAssetMinifier() : this(false) { }

    public NuglifyAssetMinifier(bool htmlAggressive)
    {
        _htmlAggressive = htmlAggressive;
    }

    public string Id => "nuglify";

    public bool CanMinify(AssetType type) =>
        type is AssetType.Css or AssetType.Js or AssetType.Html or AssetType.Svg;

    public string Minify(string content, AssetType type) =>
        type switch
        {
            AssetType.Css => MinifyCss(content),
            AssetType.Js => MinifyJs(content),
            AssetType.Html => MinifyHtml(content),
            AssetType.Svg => MinifySvg(content),
            _ => content,
        };

    private static string MinifyCss(string content)
    {
        var result = Uglify.Css(content);
        return result.HasErrors ? content : result.Code;
    }

    private static string MinifyJs(string content)
    {
        var result = Uglify.Js(content);
        return result.HasErrors ? content : result.Code;
    }

    private string MinifyHtml(string content)
    {
        // NUglify's default HtmlSettings already collapse whitespace, strip comments and
        // preserve <pre>/<textarea>/<code> + conditional comments. Only opt into removing
        // optional tags when aggressive mode is requested.
        var settings = new HtmlSettings
        {
            RemoveOptionalTags = _htmlAggressive,
        };
        var result = Uglify.Html(content, settings);
        return result.HasErrors ? content : result.Code;
    }

    private static string MinifySvg(string content)
    {
        // Strip XML comments
        var result = SvgCommentRegex().Replace(content, string.Empty);
        // Collapse whitespace between tags
        result = SvgWhitespaceRegex().Replace(result, "><");
        return result.Trim();
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex SvgCommentRegex();

    [GeneratedRegex(@">\s+<")]
    private static partial Regex SvgWhitespaceRegex();
}
