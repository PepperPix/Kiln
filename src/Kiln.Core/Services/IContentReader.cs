namespace Kiln.Services;

using Kiln.Models;

public interface IContentReader
{
    /// <summary>
    /// Reads all content files from the content directory.
    /// </summary>
    IReadOnlyList<ContentItem> ReadAll(string contentDirectory, string outputDir);
}
