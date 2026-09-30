namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Models;

/// <summary>
/// Reads content items from Markdown files.
/// </summary>
public interface IContentReader
{
    /// <summary>
    /// Reads all items of a collection from its directory, including subdirectories. A subdirectory that contains an
    /// <c>index.md</c> is read as a single item (page bundle). Files without front matter are skipped.
    /// </summary>
    /// <param name="collection">The collection to read.</param>
    /// <param name="projectPath">The project directory against which a relative collection directory is resolved.</param>
    /// <param name="plugins">The plugins whose shortcodes are expanded, or <see langword="null"/> for none.</param>
    /// <param name="warnings">A collection that receives warnings, or <see langword="null"/> to discard them.</param>
    /// <returns>The items in the order given by <see cref="ContentGroup.Sort"/>; empty when the collection directory does not exist.</returns>
    IReadOnlyList<ContentItem> ReadCollection(
        ContentGroup collection,
        string projectPath,
        IReadOnlyList<PluginDefinition>? plugins = null,
        Collection<string>? warnings = null);

    /// <summary>
    /// Reads a single content file.
    /// </summary>
    /// <param name="absoluteFilePath">The full path of the Markdown file.</param>
    /// <param name="owningCollection">The collection the item is assigned to.</param>
    /// <param name="plugins">The plugins whose shortcodes are expanded, or <see langword="null"/> for none.</param>
    /// <param name="warnings">A collection that receives warnings, or <see langword="null"/> to discard them.</param>
    /// <returns>The item.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidOperationException">The file has no valid front matter.</exception>
    ContentItem ReadSingleFile(
        string absoluteFilePath,
        ContentGroup owningCollection,
        IReadOnlyList<PluginDefinition>? plugins = null,
        Collection<string>? warnings = null);
}
