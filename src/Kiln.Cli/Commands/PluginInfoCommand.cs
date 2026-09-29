namespace Kiln.Cli.Commands;

using System.ComponentModel;
using Kiln.Services;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class PluginInfoCommand(
    INuGetPluginClient nuGetPluginClient,
    IAnsiConsole console) : AsyncCommand<PluginInfoCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<package-id>")]
        [Description("NuGet package ID to inspect.")]
        public string PackageId { get; init; } = string.Empty;

        [CommandOption("--version")]
        [Description("Package version to inspect. Defaults to latest stable release.")]
        public string? Version { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
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

        PluginInfoRenderer.Render(console, info);
        return 0;
    }
}
