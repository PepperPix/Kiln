namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class PluginRemoveCommand(
    IPluginLockFile pluginLockFile,
    IAnsiConsole console) : AsyncCommand<PluginRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Plugin directory name to remove.")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "[path]")]
        [Description("Project path. Defaults to the current directory.")]
        public string Path { get; init; } = ".";

        [CommandOption("--yes")]
        [Description("Skip the confirmation prompt.")]
        public bool Yes { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Path);
        var pluginDir = Path.Combine(projectPath, "plugins", settings.Name);

        if (Directory.Exists(pluginDir) && !settings.Yes)
        {
            var confirmed = await console.ConfirmAsync(
                prompt: $"Remove plugin '{settings.Name}' and its lock entry?",
                defaultValue: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!confirmed)
                return 1;
        }

        if (Directory.Exists(pluginDir))
            Directory.Delete(pluginDir, recursive: true);

        await pluginLockFile.RemoveAsync(projectPath, settings.Name, cancellationToken).ConfigureAwait(false);

        console.MarkupLine($"[green]Removed plugin:[/] {settings.Name}");
        console.MarkupLine("[dim]If the plugin is enabled in site.yaml, remove the matching plugin entry manually.[/]");
        return 0;
    }
}
