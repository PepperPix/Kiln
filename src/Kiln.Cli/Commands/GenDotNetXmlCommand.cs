namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class GenDotNetXmlCommand(IXmlDocGenerator generator) : AsyncCommand<GenDotNetXmlCommand.Settings>
{
    private readonly IXmlDocGenerator _generator = generator;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--xml <path>")]
        [Description("Path to the .NET XML documentation file.")]
        public string? Xml { get; init; }

        [CommandOption("--output <dir>")]
        [Description("Output directory for generated content files. Defaults to content/api-dotnet.")]
        public string Output { get; init; } = "content/api-dotnet";

        [CommandOption("--project <path>")]
        [Description("Path to the site project directory. Defaults to current directory.")]
        public string Project { get; init; } = ".";
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Xml))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --xml is required.");
            return 1;
        }

        var xmlPath = Path.GetFullPath(settings.Xml);
        if (!File.Exists(xmlPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] XML documentation file not found: {xmlPath}");
            return 1;
        }

        var projectPath = Path.GetFullPath(settings.Project);
        var outputDir = Path.IsPathRooted(settings.Output)
            ? settings.Output
            : Path.Combine(projectPath, settings.Output);

        var report = await Task.Run(
            () => _generator.Generate(xmlPath, outputDir),
            cancellationToken).ConfigureAwait(false);

        foreach (var warning in report.Warnings)
            AnsiConsole.MarkupLine($"[yellow]WARN:[/] {warning}");

        foreach (var file in report.Written)
            AnsiConsole.MarkupLine($"[green]written[/] {file}");

        foreach (var file in report.Skipped)
            AnsiConsole.MarkupLine($"[dim]skipped (adopted)[/] {file}");

        foreach (var file in report.Conflicts)
            AnsiConsole.MarkupLine($"[yellow]conflict[/] wrote .regenerated for {file}");

        AnsiConsole.MarkupLine(
            $"Done. {report.Written.Count} written, {report.Skipped.Count} skipped, {report.Conflicts.Count} conflicts.");

        var siteYaml = Path.Combine(projectPath, "site.yaml");
        if (File.Exists(siteYaml))
        {
            var siteContent = await File.ReadAllTextAsync(siteYaml, cancellationToken).ConfigureAwait(false);
            var relOutput = Path.GetRelativePath(projectPath, outputDir).Replace(Path.DirectorySeparatorChar, '/');
            if (!siteContent.Contains(relOutput, StringComparison.Ordinal))
                AnsiConsole.MarkupLine(
                    $"[yellow]WARN:[/] Add a collection for '{relOutput}' to site.yaml to include it in the build.");
        }

        return 0;
    }
}
