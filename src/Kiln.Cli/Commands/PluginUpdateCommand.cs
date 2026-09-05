namespace Kiln.Cli.Commands;

using System.ComponentModel;
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

        [CommandArgument(1, "[path]")]
        [Description("Project path. Defaults to the current directory.")]
        public string Path { get; init; } = ".";
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
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

            foreach (var plugin in entries)
            {
                await UpdateSingleEntryAsync(projectPath, plugin.Key, plugin.Value, cancellationToken).ConfigureAwait(false);
            }
            return 0;
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            console.MarkupLine("[red]ERROR:[/] Use --all or provide a plugin name.");
            return 1;
        }

        if (!entries.TryGetValue(settings.Name, out var entry))
        {
            console.MarkupLine($"[red]ERROR:[/] Plugin '{settings.Name}' has no lock entry and cannot be updated automatically — kein Lock-Eintrag.");
            return 1;
        }

        await UpdateSingleEntryAsync(projectPath, settings.Name, entry, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task UpdateSingleEntryAsync(string projectPath, string name, PluginLockEntry entry, CancellationToken cancellationToken)
    {
        console.MarkupLine($"[dim]Checking {name} ({entry.PackageId} {entry.Version})...[/]");

        var latestVersion = await nuGetPluginClient.GetLatestVersionAsync(entry.PackageId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(latestVersion))
        {
            console.MarkupLine($"[yellow]WARN:[/] No upstream version information available for {entry.PackageId}.");
            return;
        }

        if (!await nuGetPluginClient.IsUpdateAvailableAsync(entry.PackageId, entry.Version, cancellationToken).ConfigureAwait(false))
        {
            console.MarkupLine($"[green]Plugin '{name}' ist bereits aktuell.[/]");
            return;
        }

        console.MarkupLine("[yellow]IMPORTANT:[/] This plugin can inject arbitrary HTML/JavaScript into pages. Install only plugins from trusted sources.");

        var result = await nuGetPluginClient.AddAsync(entry.PackageId, latestVersion, projectPath, cancellationToken).ConfigureAwait(false);

        await pluginLockFile.SetAsync(projectPath, result.PluginName, new PluginLockEntry(
            result.PackageId,
            result.Version,
            "nuget"), cancellationToken).ConfigureAwait(false);

        console.MarkupLine($"[green]Updated plugin:[/] {result.PluginName} ({result.PackageId} {result.Version})");
    }
}
