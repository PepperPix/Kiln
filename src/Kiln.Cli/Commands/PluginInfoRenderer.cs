namespace Kiln.Cli.Commands;

using System.Globalization;
using Kiln.Services;
using Spectre.Console;

internal static class PluginInfoRenderer
{
    private const int MaxListedFiles = 20;

    public static void Render(IAnsiConsole console, PluginPackageInfo info)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(info);

        console.MarkupLine($"[bold]{Markup.Escape(info.PackageId)} {Markup.Escape(info.Version)}[/]  {PluginTrustDisplay.Markup(info.Trust)}");
        if (!info.MetadataAvailable)
            console.MarkupLine("[yellow]WARN:[/] Feed metadata (owners, prefix reservation) is not available; the package is treated as community.");

        var table = new Table().HideHeaders().Border(TableBorder.Rounded);
        table.AddColumn(string.Empty);
        table.AddColumn(string.Empty);

        AddRow(table, "Publisher", info.Owners.Count > 0 ? string.Join(", ", info.Owners) : "(not reported by the feed)");
        AddRow(table, "Authors", info.Authors.Count > 0 ? string.Join(", ", info.Authors) : "(none)");
        AddRow(table, "Description", info.Description ?? "(none)");
        AddRow(table, "License", info.License ?? "(none)");
        AddRow(table, "Project", info.ProjectUrl?.ToString() ?? "(none)");
        AddRow(table, "Plugin name", info.PluginName);
        AddRow(table, "Slots", info.Slots.Count > 0 ? string.Join(", ", info.Slots) : "(none)");
        AddRow(table, "Shortcodes", info.Shortcodes.Count > 0 ? string.Join(", ", info.Shortcodes) : "(none)");

        var inventory = info.Inventory;
        AddRow(
            table,
            "Contents",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{inventory.Scripts} script(s), {inventory.Styles} stylesheet(s), {inventory.Html} HTML file(s), {inventory.Other} other; {inventory.TotalBytes} bytes"));
        AddRow(table, "Content hash", info.ContentHash);
        console.Write(table);

        RenderFiles(console, info);
        RenderHosts(console, info);
    }

    private static void RenderFiles(IAnsiConsole console, PluginPackageInfo info)
    {
        console.MarkupLine("[bold]Files[/]");
        foreach (var file in info.Files.Take(MaxListedFiles))
            console.MarkupLine($"  {Markup.Escape(file.Path)} [dim]({file.Size.ToString(CultureInfo.InvariantCulture)} bytes)[/]");

        if (info.Files.Count > MaxListedFiles)
            console.MarkupLine($"  [dim]... and {(info.Files.Count - MaxListedFiles).ToString(CultureInfo.InvariantCulture)} more[/]");
    }

    private static void RenderHosts(IAnsiConsole console, PluginPackageInfo info)
    {
        console.MarkupLine("[bold]External hosts referenced[/] [dim](heuristic: URLs built at runtime are not detected)[/]");
        if (info.ExternalHosts.Count == 0)
        {
            console.MarkupLine("  none found");
            return;
        }

        foreach (var host in info.ExternalHosts)
            console.MarkupLine($"  {Markup.Escape(host)}");
    }

    private static void AddRow(Table table, string label, string value)
        => table.AddRow($"[dim]{Markup.Escape(label)}[/]", Markup.Escape(value));
}
