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

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Path);
        var plugins = pluginLoader.LoadPlugins(projectPath);
        var lockEntries = await pluginLockFile.ReadAsync(projectPath, cancellationToken).ConfigureAwait(false);

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Version");
        table.AddColumn("Description");
        table.AddColumn("Source");

        foreach (var plugin in plugins)
        {
            var pluginKey = Path.GetFileName(plugin.Directory);
            var source = "manuell";

            if (lockEntries.TryGetValue(pluginKey, out var entry) ||
                lockEntries.TryGetValue(plugin.Name, out entry))
            {
                source = $"{entry.PackageId} {entry.Version} ({entry.Source})";
            }

            table.AddRow(plugin.Name, plugin.Version ?? "unknown", plugin.Description ?? string.Empty, source);
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
