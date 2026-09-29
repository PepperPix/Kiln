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

        [CommandOption("--allow-any-package")]
        [Description("Allow packages that do not follow the Kiln.Plugin.* naming convention or lack the kiln-plugin tag.")]
        public bool AllowAnyPackage { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite an existing plugin directory even if it has local changes.")]
        public bool Force { get; init; }

        [CommandArgument(1, "[path]")]
        [Description("Project path. Defaults to the current directory.")]
        public string Path { get; init; } = ".";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Path);

        console.MarkupLine("[yellow]IMPORTANT:[/] This plugin can inject arbitrary HTML/JavaScript into pages. Install only from a trustworthy source.");
        if (settings.AllowAnyPackage)
            console.MarkupLine("[yellow]WARNING:[/] --allow-any-package skips the plugin naming and tag checks. The package is not recognized as a Kiln plugin.");

        PluginPackageInstallResult installResult;
        try
        {
            var options = new PluginInstallOptions
            {
                AllowAnyPackage = settings.AllowAnyPackage,
                Force = settings.Force,
                ExistingLockEntries = await pluginLockFile.ReadAsync(projectPath, cancellationToken).ConfigureAwait(false),
            };
            installResult = await nuGetPluginClient.AddAsync(settings.PackageId, settings.Version, projectPath, options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            console.MarkupLine($"[red]ERROR:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        await pluginLockFile.SetAsync(projectPath, installResult.PluginName, new PluginLockEntry(
            installResult.PackageId,
            installResult.Version,
            "nuget")
        {
            ContentHash = installResult.ContentHash,
            Unverified = installResult.Unverified,
        }, cancellationToken).ConfigureAwait(false);

        var package = $"{Markup.Escape(installResult.PackageId)} {Markup.Escape(installResult.Version)}";
        console.MarkupLine($"[green]Installed plugin:[/] {Markup.Escape(installResult.PluginName)} ({package}) at {Markup.Escape(installResult.InstallPath)}");
        console.MarkupLine("[dim]Activate it in site.yaml under the relevant collection/plugins section.[/]");
        return 0;
    }
}
