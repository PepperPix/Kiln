namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Models;

/// <summary>
/// Expands the shortcodes in Markdown content.
/// </summary>
public interface IShortcodeProcessor
{
    /// <summary>
    /// Replaces the shortcodes of the form <c>{% name arguments %}</c> with the output of the matching plugin shortcode template.
    /// Shortcodes inside code regions, and shortcodes that cannot be rendered, are left unchanged.
    /// </summary>
    /// <param name="markdown">The Markdown content.</param>
    /// <param name="plugins">The plugins that provide shortcodes.</param>
    /// <param name="warnings">A collection that receives a warning for each shortcode that could not be rendered.</param>
    /// <returns>The Markdown with the shortcodes expanded.</returns>
    string Process(string markdown, IReadOnlyList<PluginDefinition> plugins, Collection<string> warnings);
}
