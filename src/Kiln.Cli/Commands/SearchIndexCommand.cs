namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Models;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class SearchIndexCommand(
    ISearchIndexer searchIndexer,
    ISiteConfigLoader configLoader) : AsyncCommand<SearchIndexCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Path to the site project directory. Defaults to current directory.")]
        public string Path { get; init; } = ".";

        [CommandOption("--output")]
        [Description("Path to the built site directory. Defaults to the OutputDir from site config.")]
        public string? Output { get; init; }

        [CommandOption("--no-download")]
        [Description("Do not download the Pagefind binary if not found locally.")]
        public bool NoDownload { get; init; }

        [CommandOption("--extended")]
        [Description("Use the Pagefind extended binary (adds multilingual support).")]
        public bool Extended { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = System.IO.Path.GetFullPath(settings.Path);
        var config = configLoader.Load(projectPath);

        var outputDir = settings.Output is not null
            ? System.IO.Path.GetFullPath(settings.Output)
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(projectPath, config.OutputDir));

        var options = new SearchOptions
        {
            Enabled = true,
            Extended = settings.Extended || config.Search.Extended,
            BinaryPath = config.Search.BinaryPath,
        };

        var allowDownload = !settings.NoDownload;

        AnsiConsole.MarkupLine($"[dim]Indexing [blue]{Markup.Escape(outputDir)}[/] with Pagefind...[/]");

        var result = await searchIndexer
            .IndexAsync(outputDir, options, allowDownload, cancellationToken)
            .ConfigureAwait(false);

        foreach (var warning in result.Warnings)
            AnsiConsole.MarkupLine($"[yellow]WARN:[/] {Markup.Escape(warning)}");

        foreach (var error in result.Errors)
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {Markup.Escape(error)}");

        if (!result.Success)
            return 1;

        var indexDir = System.IO.Path.Combine(outputDir, "pagefind");
        AnsiConsole.MarkupLine($"[green]Search index built.[/] Index at [blue]{Markup.Escape(indexDir)}[/]");
        return 0;
    }
}
