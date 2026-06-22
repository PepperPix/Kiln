namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Models;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class DeployCommand(IDeploymentInitializer deploymentInitializer) : Command<DeployCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<target>")]
        [Description("Deployment target (github-pages, azure-swa).")]
        public required string Target { get; init; }

        [CommandOption("-p|--path")]
        [Description("Path to the site project directory. Defaults to current directory.")]
        public string Path { get; init; } = ".";
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = System.IO.Path.GetFullPath(settings.Path);
        if (!TryParseTarget(settings.Target, out var target))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Unknown deployment target: {settings.Target}. Supported: github-pages, azure-swa.");
            return 1;
        }

        try
        {
            var result = deploymentInitializer.Initialize(target, projectPath, cancellationToken);
            foreach (var createdFile in result.CreatedFiles)
            {
                var fullPath = Path.Combine(projectPath, Path.Combine(createdFile.Split('/')));
                AnsiConsole.MarkupLine($"[green]Created[/] [blue]{fullPath}[/]");
            }

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

    private static bool TryParseTarget(string target, out DeploymentTarget deploymentTarget)
    {
        switch (target.ToUpperInvariant())
        {
            case "GITHUB-PAGES":
                deploymentTarget = DeploymentTarget.GitHubPages;
                return true;
            case "AZURE-SWA":
                deploymentTarget = DeploymentTarget.AzureStaticWebApps;
                return true;
            default:
                deploymentTarget = default;
                return false;
        }
    }

    private static int WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
        return 1;
    }
}
