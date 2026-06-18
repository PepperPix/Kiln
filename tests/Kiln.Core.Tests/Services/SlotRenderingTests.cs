namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class SlotRenderingTests
{
    private readonly TemplateRenderer _renderer = new();

    [Test]
    public async Task Slot_RendersPluginPartial_WhenEnabled()
    {
        var (projectDir, themePath) = CreateSiteWithPlugin(
            pluginName: "test-plugin",
            slotName: "after_content",
            slotContent: "<div>PLUGIN CONTENT</div>",
            collectionPluginsYaml: "test-plugin:\n  enabled: true",
            globalPluginsYaml: "test-plugin:\n  priority: 1");

        try
        {
            var layout = "{{ page.content }}{{ slot 'after_content' }}";
            File.WriteAllText(Path.Combine(themePath, "layouts", "default.html"), layout);

            var collection = CreateTestCollection(projectDir);
            var item = CreateTestItem(collection);
            var site = CreateTestSite(collection);
            var plugins = new PluginLoader().LoadPlugins(projectDir);

            var result = _renderer.Render(item, site, themePath, plugins);

            await Assert.That(result).Contains("<div>PLUGIN CONTENT</div>");
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Test]
    public async Task Slot_DoesNotRender_WhenPluginNotEnabled()
    {
        var (projectDir, themePath) = CreateSiteWithPlugin(
            pluginName: "test-plugin",
            slotName: "after_content",
            slotContent: "<div>SHOULD NOT APPEAR</div>",
            collectionPluginsYaml: "test-plugin:\n  enabled: false",
            globalPluginsYaml: "test-plugin:\n  priority: 1");

        try
        {
            var layout = "{{ page.content }}{{ slot 'after_content' }}";
            File.WriteAllText(Path.Combine(themePath, "layouts", "default.html"), layout);

            var collection = CreateTestCollection(projectDir);
            var item = CreateTestItem(collection);
            var site = CreateTestSite(collection);
            var plugins = new PluginLoader().LoadPlugins(projectDir);

            var result = _renderer.Render(item, site, themePath, plugins);

            await Assert.That(result).DoesNotContain("<div>SHOULD NOT APPEAR</div>");
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Test]
    public async Task Slot_DoesNotRender_WhenCollectionHasNoPluginConfig()
    {
        var (projectDir, themePath) = CreateSiteWithPlugin(
            pluginName: "test-plugin",
            slotName: "after_content",
            slotContent: "<div>NO CONFIG</div>",
            collectionPluginsYaml: null,
            globalPluginsYaml: "test-plugin:\n  priority: 1");

        try
        {
            var layout = "{{ page.content }}{{ slot 'after_content' }}";
            File.WriteAllText(Path.Combine(themePath, "layouts", "default.html"), layout);

            var collection = CreateTestCollection(projectDir);
            var item = CreateTestItem(collection);
            var site = CreateTestSite(collection);
            var plugins = new PluginLoader().LoadPlugins(projectDir);

            var result = _renderer.Render(item, site, themePath, plugins);

            await Assert.That(result).DoesNotContain("<div>NO CONFIG</div>");
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Test]
    public async Task Slot_SortsByPriority()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-priority-{Guid.NewGuid():N}");
        var themeDir = Path.Combine(dir, "themes", "default");
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "partials"));

        // Plugin A - priority 20 (renders second)
        var pluginADir = Path.Combine(dir, "plugins", "plugin-a");
        Directory.CreateDirectory(Path.Combine(pluginADir, "slots"));
        File.WriteAllText(Path.Combine(pluginADir, "plugin.yaml"), "name: Plugin A\nslots:\n  - after_content\n");
        File.WriteAllText(Path.Combine(pluginADir, "slots", "after_content.html"), "SECOND");

        // Plugin B - priority 5 (renders first)
        var pluginBDir = Path.Combine(dir, "plugins", "plugin-b");
        Directory.CreateDirectory(Path.Combine(pluginBDir, "slots"));
        File.WriteAllText(Path.Combine(pluginBDir, "plugin.yaml"), "name: Plugin B\nslots:\n  - after_content\n");
        File.WriteAllText(Path.Combine(pluginBDir, "slots", "after_content.html"), "FIRST");

        File.WriteAllText(Path.Combine(themeDir, "layouts", "default.html"), "{{ slot 'after_content' }}");

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
                plugins:
                  plugin-a:
                    enabled: true
                  plugin-b:
                    enabled: true
            plugins:
              plugin-a:
                priority: 20
              plugin-b:
                priority: 5
            """);

        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));

        try
        {
            var plugins = new PluginLoader().LoadPlugins(dir);
            var config = new SiteConfigLoader().Load(dir);
            var collection = config.Collections["posts"];
            var item = CreateTestItem(collection);
            item.Url = new Uri("/posts/test/", UriKind.Relative);

            var result = _renderer.Render(item, config, themeDir, plugins);

            var firstIdx = result.IndexOf("FIRST", StringComparison.Ordinal);
            var secondIdx = result.IndexOf("SECOND", StringComparison.Ordinal);
            await Assert.That(firstIdx).IsLessThan(secondIdx);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Slot_EmptyString_WhenNoPlugins()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-noplugins-{Guid.NewGuid():N}");
        var themeDir = Path.Combine(dir, "themes", "default");
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));

        File.WriteAllText(Path.Combine(themeDir, "layouts", "default.html"),
            "<body>{{ page.content }}{{ slot 'after_content' }}</body>");
        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            "title: T\nbaseUrl: http://localhost\ncollections:\n  posts:\n    directory: content/posts\n");

        try
        {
            var config = new SiteConfigLoader().Load(dir);
            var collection = config.Collections["posts"];
            var item = CreateTestItem(collection);
            item.Url = new Uri("/posts/test/", UriKind.Relative);

            var result = _renderer.Render(item, config, themeDir, []);

            await Assert.That(result).IsEqualTo("<body><p>Hello</p></body>");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static (string projectDir, string themePath) CreateSiteWithPlugin(
        string pluginName,
        string slotName,
        string slotContent,
        string? collectionPluginsYaml,
        string? globalPluginsYaml)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-slot-{Guid.NewGuid():N}");
        var themeDir = Path.Combine(dir, "themes", "default");
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));

        var pluginDir = Path.Combine(dir, "plugins", pluginName);
        Directory.CreateDirectory(Path.Combine(pluginDir, "slots"));
        File.WriteAllText(Path.Combine(pluginDir, "plugin.yaml"),
            $"name: {pluginName}\nslots:\n  - {slotName}\n");
        File.WriteAllText(Path.Combine(pluginDir, "slots", $"{slotName}.html"), slotContent);

        var collectionPluginsBlock = collectionPluginsYaml is not null
            ? $"\n    plugins:\n      {collectionPluginsYaml.Replace("\n", "\n      ")}"
            : "";
        var globalPluginsBlock = globalPluginsYaml is not null
            ? $"\nplugins:\n  {globalPluginsYaml.Replace("\n", "\n  ")}"
            : "";

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $"""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts{collectionPluginsBlock}
            {globalPluginsBlock}
            """);

        return (dir, themeDir);
    }

    private static ContentGroup CreateTestCollection(string projectDir)
    {
        var config = new SiteConfigLoader().Load(projectDir);
        return config.Collections["posts"];
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

    private static SiteConfiguration CreateTestSite(ContentGroup collection) => new()
    {
        Title = "Test Site",
        BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
        Collections = new Dictionary<string, ContentGroup> { ["posts"] = collection }
    };
}
