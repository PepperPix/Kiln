namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Abstractions;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class BuildCommand(ISiteBuilder siteBuilder) : AsyncCommand<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Path to the site project directory. Defaults to current directory.")]
        public string Path { get; init; } = ".";

        [CommandOption("-d|--drafts")]
        [Description("Include draft posts in the build.")]
        public bool IncludeDrafts { get; init; }

        [CommandOption("--production")]
        [Description("Build in production mode (minify, fingerprint, link-check).")]
        public bool Production { get; init; }

        [CommandOption("-e|--environment")]
        [Description("Build environment: development (default) or production.")]
        public string? Environment { get; init; }

        [CommandOption("--release")]
        [Description("Alias for --production.")]
        public bool Release { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = System.IO.Path.GetFullPath(settings.Path);

        var environment = ResolveEnvironment(settings);

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Building site...", async _ =>
                await siteBuilder.BuildAsync(projectPath, settings.IncludeDrafts, environment, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
                AnsiConsole.MarkupLine($"[red]ERROR:[/] {error}");
            return 1;
        }

        foreach (var warning in result.Warnings)
            AnsiConsole.MarkupLine($"[yellow]WARN:[/] {warning}");

        AnsiConsole.MarkupLine(
            $"[green]Done![/] {result.RenderedFiles} files rendered in {result.Duration.TotalMilliseconds:F0}ms → [blue]{result.OutputDirectory}[/]");

        if (result.SkippedDrafts > 0)
            AnsiConsole.MarkupLine($"[dim]({result.SkippedDrafts} drafts skipped)[/]");

        return 0;
    }

    private static BuildEnvironment ResolveEnvironment(Settings settings)
    {
        if (settings.Production || settings.Release)
            return BuildEnvironment.Production;

        if (settings.Environment is not null &&
            string.Equals(settings.Environment, "production", StringComparison.OrdinalIgnoreCase))
            return BuildEnvironment.Production;

        return BuildEnvironment.Development;
    }
}
