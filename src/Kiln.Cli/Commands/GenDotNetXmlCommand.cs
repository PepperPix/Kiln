namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Models;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class GenDotNetXmlCommand(IXmlDocGenerator generator, IAnsiConsole console) : AsyncCommand<GenDotNetXmlCommand.Settings>
{
    private readonly IXmlDocGenerator _generator = generator;
    private readonly IAnsiConsole _console = console;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--xml <path>")]
        [Description("Path to the .NET XML documentation file.")]
        public string? Xml { get; init; }

        [CommandOption("--assembly <path>")]
        [Description("Path to the compiled assembly; restricts the output to publicly visible types and members.")]
        public string? Assembly { get; init; }

        [CommandOption("--output <dir>")]
        [Description("Output directory for generated content files. Defaults to content/api-dotnet.")]
        public string Output { get; init; } = "content/api-dotnet";

        [CommandOption("--project <path>")]
        [Description("Path to the site project directory. Defaults to current directory.")]
        public string Project { get; init; } = ".";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Xml))
        {
            _console.MarkupLine("[red]Error:[/] --xml is required.");
            return 1;
        }

        var xmlPath = Path.GetFullPath(settings.Xml);
        if (!File.Exists(xmlPath))
        {
            _console.MarkupLine($"[red]Error:[/] XML documentation file not found: {xmlPath}");
            return 1;
        }

        string? assemblyPath = null;
        if (!string.IsNullOrWhiteSpace(settings.Assembly))
        {
            assemblyPath = Path.GetFullPath(settings.Assembly);
            if (!File.Exists(assemblyPath))
            {
                _console.MarkupLine($"[red]Error:[/] Assembly not found: {Markup.Escape(assemblyPath)}");
                return 1;
            }
        }

        var projectPath = Path.GetFullPath(settings.Project);
        var outputDir = Path.IsPathRooted(settings.Output)
            ? settings.Output
            : Path.Combine(projectPath, settings.Output);

        DocGenReport report;
        try
        {
            report = await Task.Run(
                () => assemblyPath is null
                    ? _generator.Generate(xmlPath, outputDir)
                    : _generator.Generate(xmlPath, outputDir, new XmlDocGenerationOptions(assemblyPath)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        foreach (var warning in report.Warnings)
            _console.MarkupLine($"[yellow]WARN:[/] {warning}");

        foreach (var note in report.Notes)
            _console.MarkupLine($"[dim]{Markup.Escape(note)}[/]");

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
