namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Models;

public interface IContentReader
{
    IReadOnlyList<ContentItem> ReadCollection(
        ContentGroup collection,
        string projectPath,
        IReadOnlyList<PluginDefinition>? plugins = null,
        Collection<string>? warnings = null);

    ContentItem ReadSingleFile(
        string absoluteFilePath,
        ContentGroup owningCollection,
        IReadOnlyList<PluginDefinition>? plugins = null,
        Collection<string>? warnings = null);
}
