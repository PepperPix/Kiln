namespace Kiln.Services;

using Kiln.Models;

public interface ITemplateRenderer
{
    string Render(ContentItem item, SiteConfiguration site, string themePath);
}
