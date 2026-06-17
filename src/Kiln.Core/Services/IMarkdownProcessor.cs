namespace Kiln.Services;

public interface IMarkdownProcessor
{
    /// <summary>
    /// Converts Markdown to HTML.
    /// </summary>
    string ToHtml(string markdown);
}
