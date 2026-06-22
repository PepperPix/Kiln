namespace Kiln.Models;

public sealed class BuildOptions
{
    public bool MinifyCss { get; init; } = true;
    public bool MinifyJs { get; init; } = true;
    public bool MinifyHtml { get; init; } = true;
    public bool MinifySvg { get; init; } = true;
    public bool HtmlAggressive { get; init; }
    public bool Fingerprint { get; init; } = true;
    public bool LinkCheck { get; init; } = true;
}
