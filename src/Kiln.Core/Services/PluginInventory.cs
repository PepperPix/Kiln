namespace Kiln.Services;

/// <summary>
/// File counts by kind and the total size of a plugin.
/// </summary>
/// <param name="Scripts">Number of JavaScript files.</param>
/// <param name="Styles">Number of stylesheets.</param>
/// <param name="Html">Number of HTML files.</param>
/// <param name="Other">Number of all other files.</param>
/// <param name="TotalBytes">Total size of all files in bytes.</param>
public sealed record PluginInventory(int Scripts, int Styles, int Html, int Other, long TotalBytes);
