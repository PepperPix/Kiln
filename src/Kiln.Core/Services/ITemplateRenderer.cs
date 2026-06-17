namespace Kiln.Services;

using Kiln.Models;

public interface ITemplateRenderer
{
    /// <summary>
    /// Renders a content item through its layout template.
    /// </summary>
    string Render(ContentItem item, SiteConfiguration site, string themePath);
}
