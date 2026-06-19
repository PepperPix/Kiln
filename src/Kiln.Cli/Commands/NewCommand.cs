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
        (".config/dotnet-tools.json",                                  ".config/dotnet-tools.json"),
        ("site.yaml.template",                                         "site.yaml"),
        ("content/posts/hello-world.md.template",                      "content/posts/hello-world.md"),
        ("content/posts/01-welcome-to-kiln.md.template",               "content/posts/01-welcome-to-kiln.md"),
        ("content/posts/02-markdown-showcase.md.template",             "content/posts/02-markdown-showcase.md"),
        ("content/posts/03-code-highlighting.md.template",             "content/posts/03-code-highlighting.md"),
        ("content/posts/04-working-with-collections.md.template",      "content/posts/04-working-with-collections.md"),
        ("content/posts/05-taxonomies-and-tags.md.template",           "content/posts/05-taxonomies-and-tags.md"),
        ("content/posts/_06_page_bundles_and_images/index.md.template", "content/posts/06-page-bundles-and-images/index.md"),
        ("content/posts/_06_page_bundles_and_images/kiln-architecture.svg", "content/posts/06-page-bundles-and-images/kiln-architecture.svg"),
        ("content/posts/07-theming-with-kiln.md.template",             "content/posts/07-theming-with-kiln.md"),
        ("content/posts/08-deploy-your-site.md.template",              "content/posts/08-deploy-your-site.md"),
        ("content/posts/09-plugins-and-slots.md.template",             "content/posts/09-plugins-and-slots.md"),
        ("content/posts/10-configuration-deep-dive.md.template",       "content/posts/10-configuration-deep-dive.md"),
        ("content/posts/11-performance-tips.md.template",              "content/posts/11-performance-tips.md"),
        ("content/posts/12-whats-next.md.template",                    "content/posts/12-whats-next.md"),
        ("content/pages/about.md.template",                            "content/pages/about.md"),
        ("content/pages/privacy.md.template",                          "content/pages/privacy.md"),
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
            ["{{DATE-0}}"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-1}}"] = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-2}}"] = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-3}}"] = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-4}}"] = DateTime.Now.AddDays(-4).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-5}}"] = DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-6}}"] = DateTime.Now.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-7}}"] = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-8}}"] = DateTime.Now.AddDays(-8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-9}}"] = DateTime.Now.AddDays(-9).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{DATE-10}}"] = DateTime.Now.AddDays(-10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{ID1}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID2}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID3}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID4}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID5}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID6}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID7}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID8}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID9}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID10}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID11}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID12}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID13}}"] = Guid.NewGuid().ToString("D"),
            ["{{ID14}}"] = Guid.NewGuid().ToString("D"),
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
