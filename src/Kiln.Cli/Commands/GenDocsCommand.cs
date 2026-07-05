namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class GenDocsCommand(IOpenApiDocGenerator generator, IAnsiConsole console) : AsyncCommand<GenDocsCommand.Settings>
{
    private readonly IOpenApiDocGenerator _generator = generator;
    private readonly IAnsiConsole _console = console;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--openapi <spec>")]
        [Description("Path to the OpenAPI spec file (JSON or YAML).")]
        public string? OpenApiSpec { get; init; }

        [CommandOption("--output <dir>")]
        [Description("Output directory for generated content files. Defaults to content/api.")]
        public string Output { get; init; } = "content/api";

        [CommandOption("--project <path>")]
        [Description("Path to the site project directory. Defaults to current directory.")]
        public string Project { get; init; } = ".";
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.OpenApiSpec))
        {
            _console.MarkupLine("[red]Error:[/] --openapi is required.");
            return 1;
        }

        var specPath = Path.GetFullPath(settings.OpenApiSpec);
        if (!File.Exists(specPath))
        {
            _console.MarkupLine($"[red]Error:[/] OpenAPI spec not found: {specPath}");
            return 1;
        }

        var projectPath = Path.GetFullPath(settings.Project);
        var outputDir = Path.IsPathRooted(settings.Output)
            ? settings.Output
            : Path.Combine(projectPath, settings.Output);

        var report = await Task.Run(
            () => _generator.Generate(specPath, outputDir),
            cancellationToken).ConfigureAwait(false);

        foreach (var warning in report.Warnings)
            _console.MarkupLine($"[yellow]WARN:[/] {warning}");

        foreach (var file in report.Written)
            _console.MarkupLine($"[green]written[/] {file}");

        foreach (var file in report.Skipped)
            _console.MarkupLine($"[dim]skipped (adopted)[/] {file}");

        foreach (var file in report.Conflicts)
            _console.MarkupLine($"[yellow]conflict[/] wrote .regenerated for {file}");

        _console.MarkupLine(
            $"Done. {report.Written.Count} written, {report.Skipped.Count} skipped, {report.Conflicts.Count} conflicts.");

        var siteYaml = Path.Combine(projectPath, "site.yaml");
        if (File.Exists(siteYaml))
        {
            var siteContent = await File.ReadAllTextAsync(siteYaml, cancellationToken).ConfigureAwait(false);
            var relOutput = Path.GetRelativePath(projectPath, outputDir).Replace(Path.DirectorySeparatorChar, '/');
            if (!siteContent.Contains(relOutput, StringComparison.Ordinal))
                _console.MarkupLine(
                    $"[yellow]WARN:[/] Add a collection for '{relOutput}' to site.yaml to include it in the build.");
        }

        return 0;
    }
}
