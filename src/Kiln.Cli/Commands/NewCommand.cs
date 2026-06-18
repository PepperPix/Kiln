namespace Kiln.Cli.Commands;

using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;

public sealed class NewCommand : Command<NewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name of the new site project (becomes directory name).")]
        public required string Name { get; init; }
    }

    // Paths relative to Templates/default/ within the assembly embedded resources.
    // Files ending in .template have placeholders replaced; the .template extension is stripped.
    private static readonly (string Resource, string Target)[] TemplateEntries =
    [
        ("site.yaml.template",                                         "site.yaml"),
        ("content/posts/hello-world.md.template",                      "content/posts/hello-world.md"),
        ("content/pages/about.md.template",                            "content/pages/about.md"),
        ("themes/default/layouts/default.html",                        "themes/default/layouts/default.html"),
        ("themes/default/layouts/post.html",                           "themes/default/layouts/post.html"),
        ("themes/default/layouts/posts-index.html",                    "themes/default/layouts/posts-index.html"),
        ("themes/default/layouts/taxonomy.html",                       "themes/default/layouts/taxonomy.html"),
        ("themes/default/layouts/taxonomy-index.html",                 "themes/default/layouts/taxonomy-index.html"),
        ("themes/default/partials/head.html",                          "themes/default/partials/head.html"),
        ("themes/default/partials/header.html",                        "themes/default/partials/header.html"),
        ("themes/default/partials/footer.html",                        "themes/default/partials/footer.html"),
        ("themes/default/static/css/style.css",                        "themes/default/static/css/style.css"),
        ("themes/default/static/css/prism-ember.css",                  "themes/default/static/css/prism-ember.css"),
        ("themes/default/static/favicon.svg",                          "themes/default/static/favicon.svg"),
    ];

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetFullPath(settings.Name);

        if (Directory.Exists(projectPath) && Directory.EnumerateFileSystemEntries(projectPath).Any())
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory '{settings.Name}' already exists and is not empty.");
            return 1;
        }

        Directory.CreateDirectory(projectPath);

        var assembly = typeof(NewCommand).Assembly;
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{NAME}}"] = settings.Name,
            ["{{DATE}}"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{ID1}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID2}}"] = Guid.NewGuid().ToString("D"),
        };

        foreach (var (resource, target) in TemplateEntries)
        {
            var isTemplate = resource.EndsWith(".template", StringComparison.Ordinal);
            var targetPath = Path.Combine(projectPath, Path.Combine(target.Split('/')));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var content = ReadEmbeddedResource(assembly, resource);
            if (isTemplate)
            {
                foreach (var (placeholder, value) in replacements)
                    content = content.Replace(placeholder, value, StringComparison.Ordinal);
            }

            File.WriteAllText(targetPath, content, Encoding.UTF8);
        }

        AnsiConsole.MarkupLine($"[green]Created[/] new site at [blue]{projectPath}[/]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("Next steps:");
        AnsiConsole.MarkupLine($"  cd {settings.Name}");
        AnsiConsole.MarkupLine("  kiln serve");

        return 0;
    }

    private static string ReadEmbeddedResource(Assembly assembly, string relativeToDefault)
    {
        var resourceName = "Kiln.Cli.Templates.default." + relativeToDefault.Replace('/', '.');
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource not found: {resourceName}. " +
                $"Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
