namespace Kiln.Services;

public interface IMarkdownProcessor
{
    /// <summary>
    /// Converts Markdown to HTML.
    /// When <paramref name="assetBasePath"/> is set (Page Bundle), relative image URLs
    /// are rewritten to the given base path.
    /// </summary>
    string ToHtml(string markdown, string? assetBasePath = null);

    /// <summary>
    /// Converts Markdown to plain text (no Markdown/HTML markup), suitable for teasers/excerpts.
    /// </summary>
    string ToPlainText(string markdown);
}
