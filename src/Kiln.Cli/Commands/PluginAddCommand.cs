namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class PluginAddCommand(
    INuGetPluginClient nuGetPluginClient,
    IPluginLockFile pluginLockFile,
    IAnsiConsole console) : AsyncCommand<PluginAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<package-id>")]
        [Description("NuGet package ID to install.")]
        public string PackageId { get; init; } = string.Empty;

        [CommandOption("--version")]
        [Description("Package version to install. Defaults to latest stable release.")]
        public string? Version { get; init; }

        [CommandArgument(1, "[path]")]
        [Description("Project path. Defaults to the current directory.")]
        public string Path { get; init; } = ".";
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Path);

        console.MarkupLine("[yellow]IMPORTANT:[/] This plugin can inject arbitrary HTML/JavaScript into pages. Install only from a trustworthy source.");

        var installResult = await nuGetPluginClient.AddAsync(settings.PackageId, settings.Version, projectPath, cancellationToken).ConfigureAwait(false);

        await pluginLockFile.SetAsync(projectPath, installResult.PluginName, new PluginLockEntry(
            installResult.PackageId,
            installResult.Version,
            "nuget"), cancellationToken).ConfigureAwait(false);

        console.MarkupLine($"[green]Installed plugin:[/] {installResult.PluginName} ({installResult.PackageId} {installResult.Version}) at {installResult.InstallPath}");
        console.MarkupLine("[dim]Activate it in site.yaml under the relevant collection/plugins section.[/]");
        return 0;
    }
}
