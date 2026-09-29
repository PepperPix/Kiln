namespace Kiln.Cli.Tests;

using Kiln.Models;
using Kiln.Services;

internal static class PluginInfoFactory
{
    public static PluginPackageInfo Create(
        string packageId,
        string version,
        PluginTrustLevel trust,
        IReadOnlyList<string>? hosts = null,
        string? description = "A plugin")
        => new(
            packageId,
            version,
            trust,
            Owners: trust == PluginTrustLevel.FirstParty ? ["PepperPix"] : ["someone"],
            Authors: ["Author"],
            Description: description,
            License: "MIT",
            ProjectUrl: new Uri("https://example.com/project"),
            PluginName: "widget",
            Slots: ["body_end"],
            Shortcodes: ["email"],
            Files: [new PluginFileInfo("plugin.yaml", 12), new PluginFileInfo("static/a.js", 100)],
            Inventory: new PluginInventory(Scripts: 1, Styles: 0, Html: 0, Other: 1, TotalBytes: 112),
            ExternalHosts: hosts ?? [],
            ContentHash: "hash-" + version,
            MetadataAvailable: true);
}
