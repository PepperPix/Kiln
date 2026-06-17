namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class ServeCommand(IDevServer devServer) : AsyncCommand<ServeCommand.Settings>
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

        AnsiConsole.MarkupLine($"[green]Serving[/] at [blue]http://localhost:{settings.Port}/[/]");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop.[/]");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await devServer.RunAsync(projectPath, settings.Port, settings.IncludeDrafts, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("\n[dim]Server stopped.[/]");
        }

        return 0;
    }
}
