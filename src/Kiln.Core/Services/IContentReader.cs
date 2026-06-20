namespace Kiln.Services;

using Kiln.Models;

public interface IContentReader
{
    IReadOnlyList<ContentItem> ReadCollection(ContentGroup collection, string projectPath);

    ContentItem ReadSingleFile(string absoluteFilePath, ContentGroup owningCollection);
}
