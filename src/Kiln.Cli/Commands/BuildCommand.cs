namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Abstractions;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class BuildCommand(
    ISiteBuilder siteBuilder,
    ISiteConfigLoader configLoader,
    ISearchIndexer searchIndexer,
    IAnsiConsole console) : AsyncCommand<BuildCommand.Settings>
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

#pragma warning disable S3996 // CLI validation requires the raw string so custom error output can be emitted before build starts.
        [CommandOption("--base-url")]
        [Description("Override the site's baseUrl for this build (for example, a GitHub Pages project URL).")]
        public string? BaseUrl { get; init; }
#pragma warning restore S3996

        [CommandOption("--no-search")]
        [Description("Skip building the search index even if search is enabled in site config.")]
        public bool NoSearch { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = System.IO.Path.GetFullPath(settings.Path);
        var environment = ResolveEnvironment(settings);

        if (!string.IsNullOrWhiteSpace(settings.BaseUrl)
            && (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var parsedBaseUrl) || !parsedBaseUrl.IsAbsoluteUri))
        {
            console.MarkupLine("[red]ERROR:[/] --base-url must be a valid absolute URL, for example https://example.com/reponame");
            return 1;
        }

        Uri? baseUrlOverride = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? null
            : new Uri($"{settings.BaseUrl.TrimEnd('/')}/", UriKind.Absolute);

        var result = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Building site...", async _ =>
                await siteBuilder.BuildAsync(projectPath, settings.IncludeDrafts, environment, baseUrlOverride, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
                console.MarkupLine($"[red]ERROR:[/] {error}");
            return 1;
        }

        foreach (var warning in result.Warnings)
            console.MarkupLine($"[yellow]WARN:[/] {warning}");

        console.MarkupLine(
            $"[green]Done![/] {result.RenderedFiles} files rendered in {result.Duration.TotalMilliseconds:F0}ms → [blue]{result.OutputDirectory}[/]");

        if (result.SkippedDrafts > 0)
            console.MarkupLine($"[dim]({result.SkippedDrafts} drafts skipped)[/]");

        if (!settings.NoSearch)
        {
            var config = configLoader.Load(projectPath);
            if (config.Search.Enabled)
            {
                console.MarkupLine("[dim]Building search index...[/]");
                var searchResult = await searchIndexer
                    .IndexAsync(result.OutputDirectory, config.Search, allowDownload: true, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var warning in searchResult.Warnings)
                    console.MarkupLine($"[yellow]WARN (search):[/] {warning}");

                if (!searchResult.Success)
                {
                    foreach (var error in searchResult.Errors)
                        console.MarkupLine($"[yellow]WARN:[/] Search index failed: {error}");
                }
                else
                {
                    console.MarkupLine($"[green]Search index built.[/]");
                }
            }
        }

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
