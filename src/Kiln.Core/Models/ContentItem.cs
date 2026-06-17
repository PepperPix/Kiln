namespace Kiln.Models;

/// <summary>
/// A single content item (post or page) with parsed front matter and body.
/// </summary>
public sealed class ContentItem
{
    public required string SourcePath { get; init; }
    public required string RelativePath { get; init; }
    public required FrontMatter FrontMatter { get; init; }
    public required string RawContent { get; init; }
    public required string HtmlContent { get; init; }
    public required string OutputPath { get; init; }
    public Uri Url
    {
        get
        {
            var sep = Path.AltDirectorySeparatorChar;
            return new Uri(
                $"{sep}{OutputPath.Replace(Path.DirectorySeparatorChar, sep).TrimEnd(sep)}",
                UriKind.Relative);
        }
    }
}
