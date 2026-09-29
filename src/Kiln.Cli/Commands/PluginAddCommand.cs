namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Models;
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

        [CommandOption("--force")]
        [Description("Overwrite an existing plugin directory even if it has local changes.")]
        public bool Force { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Install community plugins without asking for confirmation.")]
        public bool Yes { get; init; }

        [CommandArgument(1, "[path]")]
        [Description("Project path. Defaults to the current directory.")]
        public string Path { get; init; } = ".";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Path);

        console.MarkupLine("[yellow]IMPORTANT:[/] This plugin can inject arbitrary HTML/JavaScript into pages. Install only from a trustworthy source.");

        var version = settings.Version;
        if (!settings.Yes)
        {
            PluginPackageInfo info;
            try
            {
                info = await nuGetPluginClient.GetInfoAsync(settings.PackageId, settings.Version, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                console.MarkupLine($"[red]ERROR:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }

            // Install exactly the version that was shown.
            version = info.Version;
            if (info.Trust != PluginTrustLevel.FirstParty && !await ConfirmCommunityInstallAsync(info, cancellationToken).ConfigureAwait(false))
                return 1;
        }

        PluginPackageInstallResult installResult;
        try
        {
            var options = new PluginInstallOptions
            {
                Force = settings.Force,
                ExistingLockEntries = await pluginLockFile.ReadAsync(projectPath, cancellationToken).ConfigureAwait(false),
            };
            installResult = await nuGetPluginClient.AddAsync(settings.PackageId, version, projectPath, options, cancellationToken).ConfigureAwait(false);
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
            Trust = installResult.Trust,
        }, cancellationToken).ConfigureAwait(false);

        var package = $"{Markup.Escape(installResult.PackageId)} {Markup.Escape(installResult.Version)}";
        console.MarkupLine($"[green]Installed plugin:[/] {Markup.Escape(installResult.PluginName)} ({package}) at {Markup.Escape(installResult.InstallPath)}");
        console.MarkupLine($"[dim]Trust level: {PluginTrustDisplay.Label(installResult.Trust)}[/]");
        console.MarkupLine("[dim]Activate it in site.yaml under the relevant collection/plugins section.[/]");
        return 0;
    }

    private async Task<bool> ConfirmCommunityInstallAsync(PluginPackageInfo info, CancellationToken cancellationToken)
    {
        PluginInfoRenderer.Render(console, info);

        if (!console.Profile.Capabilities.Interactive)
        {
            console.MarkupLine("[red]ERROR:[/] Confirmation required for community plugins. Re-run with --yes.");
            return false;
        }

        var confirmed = await console.ConfirmAsync(
            prompt: $"Install community plugin '{Markup.Escape(info.PackageId)}' {Markup.Escape(info.Version)}? It can inject HTML/JavaScript into every page of your site.",
            defaultValue: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!confirmed)
            console.MarkupLine("[yellow]Installation cancelled.[/]");

        return confirmed;
    }
}
