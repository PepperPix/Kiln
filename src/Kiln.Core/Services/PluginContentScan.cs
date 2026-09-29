namespace Kiln.Services;

/// <summary>
/// The result of scanning a plugin directory.
/// </summary>
/// <param name="Files">All files, ordered by relative path.</param>
/// <param name="Inventory">File counts and total size.</param>
/// <param name="ExternalHosts">Sorted, distinct host names referenced by scripts, styles and markup (heuristic).</param>
public sealed record PluginContentScan(
    IReadOnlyList<PluginFileInfo> Files,
    PluginInventory Inventory,
    IReadOnlyList<string> ExternalHosts);
