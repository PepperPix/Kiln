namespace Kiln.Services;

using Kiln.Models;

public interface IPermalinkGenerator
{
    Uri Generate(ContentItem item, ContentGroup collection);
}
