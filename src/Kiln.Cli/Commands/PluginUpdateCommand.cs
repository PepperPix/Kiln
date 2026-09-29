namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Models;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class PluginUpdateCommand(
    INuGetPluginClient nuGetPluginClient,
    IPluginLockFile pluginLockFile,
    IAnsiConsole console) : AsyncCommand<PluginUpdateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Plugin directory name to update.")]
        public string? Name { get; init; }

        [CommandOption("--all")]
        [Description("Update all plugins recorded in .kiln/plugins.lock.json.")]
        public bool All { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite a plugin directory even if it has local changes.")]
        public bool Force { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Update without asking for confirmation when a plugin adds external hosts or loses first-party status.")]
        public bool Yes { get; init; }

        [CommandArgument(1, "[path]")]
        [Description("Project path. Defaults to the current directory.")]
        public string Path { get; init; } = ".";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Path);
        var entries = await pluginLockFile.ReadAsync(projectPath, cancellationToken).ConfigureAwait(false);

        if (settings.All)
        {
            if (entries.Count == 0)
            {
                console.MarkupLine("[yellow]No plugins are locked for this project.[/]");
                return 0;
            }

            var exitCode = 0;
            foreach (var plugin in entries)
            {
                if (!await UpdateSingleEntryAsync(projectPath, plugin.Key, plugin.Value, settings, cancellationToken).ConfigureAwait(false))
                    exitCode = 1;
            }
            return exitCode;
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            console.MarkupLine("[red]ERROR:[/] Use --all or provide a plugin name.");
            return 1;
        }

        if (!entries.TryGetValue(settings.Name, out var entry))
        {
            console.MarkupLine($"[red]ERROR:[/] Plugin '{Markup.Escape(settings.Name)}' has no lock entry and cannot be updated automatically — kein Lock-Eintrag.");
            return 1;
        }

        return await UpdateSingleEntryAsync(projectPath, settings.Name, entry, settings, cancellationToken).ConfigureAwait(false) ? 0 : 1;
    }

    private async Task<bool> UpdateSingleEntryAsync(string projectPath, string name, PluginLockEntry entry, Settings settings, CancellationToken cancellationToken)
    {
        var displayName = Markup.Escape(name);
        var packageId = Markup.Escape(entry.PackageId);
        console.MarkupLine($"[dim]Checking {displayName} ({packageId} {Markup.Escape(entry.Version)})...[/]");

        var latestVersion = await nuGetPluginClient.GetLatestVersionAsync(entry.PackageId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(latestVersion))
        {
            console.MarkupLine($"[yellow]WARN:[/] No upstream version information available for {packageId}.");
            return true;
        }

        if (!await nuGetPluginClient.IsUpdateAvailableAsync(entry.PackageId, entry.Version, cancellationToken).ConfigureAwait(false))
        {
            console.MarkupLine($"[green]Plugin '{displayName}' ist bereits aktuell.[/]");
            return true;
        }

        console.MarkupLine("[yellow]IMPORTANT:[/] This plugin can inject arbitrary HTML/JavaScript into pages. Install only plugins from trusted sources.");

        if (!settings.Yes && !await ConfirmSignificantChangeAsync(projectPath, name, entry, latestVersion, cancellationToken).ConfigureAwait(false))
            return false;

        PluginPackageInstallResult result;
        try
        {
            var options = new PluginInstallOptions
            {
                Force = settings.Force,
                ExistingLockEntries = await pluginLockFile.ReadAsync(projectPath, cancellationToken).ConfigureAwait(false),
            };
            result = await nuGetPluginClient.AddAsync(entry.PackageId, latestVersion, projectPath, options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine($"[red]ERROR:[/] Could not update '{displayName}': {Markup.Escape(ex.Message)}");
            return false;
        }

        await pluginLockFile.SetAsync(projectPath, result.PluginName, new PluginLockEntry(
            result.PackageId,
            result.Version,
            "nuget")
        {
            ContentHash = result.ContentHash,
            Trust = result.Trust,
        }, cancellationToken).ConfigureAwait(false);

        console.MarkupLine($"[green]Updated plugin:[/] {Markup.Escape(result.PluginName)} ({Markup.Escape(result.PackageId)} {Markup.Escape(result.Version)}) at {Markup.Escape(result.InstallPath)}");
        return true;
    }

    private async Task<bool> ConfirmSignificantChangeAsync(string projectPath, string name, PluginLockEntry entry, string latestVersion, CancellationToken cancellationToken)
    {
        var displayName = Markup.Escape(name);
        PluginPackageInfo info;
        try
        {
            info = await nuGetPluginClient.GetInfoAsync(entry.PackageId, latestVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine($"[red]ERROR:[/] Could not update '{displayName}': {Markup.Escape(ex.Message)}");
            return false;
        }

        console.MarkupLine($"{displayName}: {Markup.Escape(entry.Version)} -> {Markup.Escape(info.Version)}");

        var installedHosts = ReadInstalledHosts(projectPath, name);
        var newHosts = info.ExternalHosts.Where(host => !installedHosts.Contains(host)).ToList();
        var trustDropped = entry.Trust == PluginTrustLevel.FirstParty && info.Trust != PluginTrustLevel.FirstParty;
        if (newHosts.Count == 0 && !trustDropped)
            return true;

        if (newHosts.Count > 0)
            console.MarkupLine($"[yellow]This version references new external hosts:[/] {Markup.Escape(string.Join(", ", newHosts))}");

        if (trustDropped)
            console.MarkupLine("[yellow]This package is no longer first-party; its trust level dropped to community.[/]");

        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine($"[red]ERROR:[/] Confirmation required to update '{displayName}'. Re-run with --yes.");
            return false;
        }

        var confirmed = await console.ConfirmAsync(
            prompt: $"Update '{displayName}' to {Markup.Escape(info.Version)}?",
            defaultValue: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!confirmed)
            console.MarkupLine($"[yellow]Update of '{displayName}' skipped.[/]");

        return confirmed;
    }

    private static HashSet<string> ReadInstalledHosts(string projectPath, string name)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!PluginNames.IsSafeDirectoryName(name))
            return hosts;

        var directory = Path.Combine(projectPath, "plugins", name);
        if (Directory.Exists(directory))
            hosts.UnionWith(PluginContentInspector.Scan(directory).ExternalHosts);

        return hosts;
    }
}
