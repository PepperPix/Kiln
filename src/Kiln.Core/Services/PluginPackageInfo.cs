namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Describes a plugin package without installing it.
/// </summary>
/// <param name="PackageId">The NuGet package ID.</param>
/// <param name="Version">The resolved package version.</param>
/// <param name="Trust">The trust level of the package.</param>
/// <param name="Owners">Package owners on the feed; empty if the feed does not report them.</param>
/// <param name="Authors">Authors from the package metadata.</param>
/// <param name="Description">Package description, if any.</param>
/// <param name="License">License expression, file or URL, if any.</param>
/// <param name="ProjectUrl">Project URL, if any.</param>
/// <param name="PluginName">Plugin name from <c>plugin.yaml</c> (the installation directory name).</param>
/// <param name="Slots">Slots declared in <c>plugin.yaml</c>.</param>
/// <param name="Shortcodes">Shortcodes declared in <c>plugin.yaml</c>.</param>
/// <param name="Files">Files shipped in the package content.</param>
/// <param name="Inventory">File counts and total size.</param>
/// <param name="ExternalHosts">Sorted, distinct external hosts referenced by the content (heuristic).</param>
/// <param name="ContentHash">The content hash <c>plugin add</c> would record in the lock file.</param>
/// <param name="MetadataAvailable"><c>false</c> if the feed metadata (owners, prefix reservation) could not be retrieved; the trust level then falls back to community.</param>
public sealed record PluginPackageInfo(
    string PackageId,
    string Version,
    PluginTrustLevel Trust,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> Authors,
    string? Description,
    string? License,
    Uri? ProjectUrl,
    string PluginName,
    IReadOnlyList<string> Slots,
    IReadOnlyList<string> Shortcodes,
    IReadOnlyList<PluginFileInfo> Files,
    PluginInventory Inventory,
    IReadOnlyList<string> ExternalHosts,
    string ContentHash,
    bool MetadataAvailable);
