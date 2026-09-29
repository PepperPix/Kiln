namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class PluginListCommand(
    IPluginLoader pluginLoader,
    IPluginLockFile pluginLockFile,
    IAnsiConsole console) : AsyncCommand<PluginListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Project path. Defaults to the current directory.")]
        public string Path { get; init; } = ".";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Path);
        var plugins = pluginLoader.LoadPlugins(projectPath);
        var lockEntries = await pluginLockFile.ReadAsync(projectPath, cancellationToken).ConfigureAwait(false);

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Description");
        table.AddColumn("Source");
        table.AddColumn("Trust");

        foreach (var plugin in plugins)
        {
            var pluginKey = Path.GetFileName(plugin.Directory);
            var source = "manuell";
            var trust = "manual";

            if (lockEntries.TryGetValue(pluginKey, out var entry) ||
                lockEntries.TryGetValue(plugin.Name, out entry))
            {
                trust = entry.Trust is { } level ? PluginTrustDisplay.Label(level) : "community (unknown)";
                source = $"{entry.PackageId} {entry.Version} ({entry.Source})";
                if (entry.ContentHash is not null && !string.Equals(entry.ContentHash, PluginContentHasher.ComputeDirectoryHash(plugin.Directory), StringComparison.OrdinalIgnoreCase))
                    source += " modified";
            }

            table.AddRow(
                Markup.Escape(plugin.Name),
                Markup.Escape(plugin.Version ?? "unknown"),
                Markup.Escape(plugin.Description ?? string.Empty),
                Markup.Escape(source),
                Markup.Escape(trust));
        }

        if (plugins.Count == 0)
        {
            console.MarkupLine("[yellow]No plugins found in the project.[/]");
            return 0;
        }

        console.Write(table);
        return 0;
    }
}
