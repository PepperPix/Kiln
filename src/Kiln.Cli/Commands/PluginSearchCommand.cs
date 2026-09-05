namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class PluginSearchCommand(
    INuGetPluginClient pluginClient,
    IAnsiConsole console) : AsyncCommand<PluginSearchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<query>")]
        [Description("Search term or package name fragment.")]
        public string Query { get; init; } = string.Empty;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var results = await pluginClient.SearchAsync(settings.Query, cancellationToken).ConfigureAwait(false);

        if (results.Count == 0)
        {
            console.MarkupLine("[yellow]No matching Kiln plugins found.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Package ID");
        table.AddColumn("Version");
        table.AddColumn("Description");

        foreach (var result in results)
        {
            table.AddRow(result.Id, result.Version, result.Description);
        }

        console.Write(table);
        return 0;
    }
}
