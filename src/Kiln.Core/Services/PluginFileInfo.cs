namespace Kiln.Services;

/// <summary>
/// A file inside a plugin package or an installed plugin directory.
/// </summary>
/// <param name="Path">Path relative to the plugin root, using '/' separators.</param>
/// <param name="Size">Size in bytes.</param>
public sealed record PluginFileInfo(string Path, long Size);
