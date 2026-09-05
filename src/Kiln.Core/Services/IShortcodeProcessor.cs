namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Models;

public interface IShortcodeProcessor
{
    string Process(string markdown, IReadOnlyList<PluginDefinition> plugins, Collection<string> warnings);
}
