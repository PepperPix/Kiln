namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class ServeCommand(IDevServer devServer, IAnsiConsole console) : AsyncCommand<ServeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Path to the site project directory. Defaults to current directory.")]
        public string Path { get; init; } = ".";

        [CommandOption("-p|--port")]
        [Description("Port for the local server.")]
        [DefaultValue(5555)]
        public int Port { get; init; }

        [CommandOption("-d|--drafts")]
        [Description("Include draft posts.")]
        public bool IncludeDrafts { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = System.IO.Path.GetFullPath(settings.Path);

        console.MarkupLine($"[green]Serving[/] at [blue]http://localhost:{settings.Port}/[/]");
        console.MarkupLine("[dim]Press Ctrl+C to stop.[/]");

        try
        {
            await devServer.RunAsync(projectPath, settings.Port, settings.IncludeDrafts, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            console.MarkupLine("\n[dim]Server stopped.[/]");
        }

        return 0;
    }
}
