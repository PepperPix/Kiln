namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class NewCommand(IScaffolder scaffolder) : Command<NewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name of the new site project (becomes directory name).")]
        public required string Name { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(settings.Name))!;
            var result = scaffolder.CreateSite(settings.Name, outputDirectory, cancellationToken);

            AnsiConsole.MarkupLine($"[green]Created[/] new site at [blue]{result.ProjectPath}[/]");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Next steps:");
            AnsiConsole.MarkupLine($"  cd {settings.Name}");
            AnsiConsole.MarkupLine("  kiln serve");

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            return WriteError(ex.Message);
        }
        catch (IOException ex)
        {
            return WriteError(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return WriteError(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return WriteError(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return WriteError(ex.Message);
        }
    }

    private static int WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
        return 1;
    }
}
