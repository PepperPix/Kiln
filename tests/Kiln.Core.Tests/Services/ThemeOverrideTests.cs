namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class ThemeOverrideTests
{
    private readonly TemplateRenderer _renderer = new();

    [Test]
    public async Task Slot_UsesThemeOverride_WhenThemeFileExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-override-{Guid.NewGuid():N}");
        var themeDir = Path.Combine(dir, "themes", "default");

        // Plugin default slot
        var pluginDir = Path.Combine(dir, "plugins", "disqus");
        Directory.CreateDirectory(Path.Combine(pluginDir, "slots"));
        await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"),
            "name: Disqus\nslots:\n  - after_content\n");
        await File.WriteAllTextAsync(Path.Combine(pluginDir, "slots", "after_content.html"),
            "<div>PLUGIN DEFAULT</div>");

        // Theme override
        var themePluginSlotDir = Path.Combine(themeDir, "plugins", "disqus", "slots");
        Directory.CreateDirectory(themePluginSlotDir);
        await File.WriteAllTextAsync(Path.Combine(themePluginSlotDir, "after_content.html"),
            "<div>THEME OVERRIDE</div>");

        Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        await File.WriteAllTextAsync(Path.Combine(themeDir, "layouts", "default.html"),
            "{{ slot 'after_content' }}");

        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"),
            """
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
                plugins:
                  disqus:
                    enabled: true
            plugins:
              disqus:
                priority: 1
            """);

        try
        {
            var plugins = new PluginLoader().LoadPlugins(dir);
            var config = new SiteConfigLoader().Load(dir);
            var collection = config.Collections["posts"];
            var item = CreateTestItem(collection);
            var shared = SharedRenderContext.Build(config, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, config, themeDir, plugins);

            await Assert.That(result).Contains("<div>THEME OVERRIDE</div>");
            await Assert.That(result).DoesNotContain("<div>PLUGIN DEFAULT</div>");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Slot_UsesPluginDefault_WhenNoThemeOverride()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-nooverride-{Guid.NewGuid():N}");
        var themeDir = Path.Combine(dir, "themes", "default");

        var pluginDir = Path.Combine(dir, "plugins", "analytics");
        Directory.CreateDirectory(Path.Combine(pluginDir, "slots"));
        await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"),
            "name: Analytics\nslots:\n  - head\n");
        await File.WriteAllTextAsync(Path.Combine(pluginDir, "slots", "head.html"),
            "<script>ANALYTICS</script>");

        Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        await File.WriteAllTextAsync(Path.Combine(themeDir, "layouts", "default.html"),
            "{{ slot 'head' }}{{ page.content }}");

        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"),
            """
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
                plugins:
                  analytics:
                    enabled: true
            plugins:
              analytics:
                priority: 1
            """);

        try
        {
            var plugins = new PluginLoader().LoadPlugins(dir);
            var config = new SiteConfigLoader().Load(dir);
            var collection = config.Collections["posts"];
            var item = CreateTestItem(collection);
            var shared = SharedRenderContext.Build(config, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, config, themeDir, plugins);

            await Assert.That(result).Contains("<script>ANALYTICS</script>");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static ContentItem CreateTestItem(ContentGroup collection) => new()
    {
        SourcePath = "/test/content/test.md",
        RelativePath = "test.md",
        Title = "Test Post",
        Slug = "test-post",
        RawContent = "Hello",
        HtmlContent = "<p>Hello</p>",
        Url = new Uri("/posts/test-post/", UriKind.Relative),
        OutputPath = "posts/test-post/index.html",
        Collection = collection
    };
}
