namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PluginAssetTests
{
    [Test]
    public async Task BuildAsync_CopiesPluginAssetsToAssetsPluginsDir()
    {
        var dir = CreateSiteWithPluginAssets();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(
                Path.Combine(dir, "_site", "assets", "plugins", "my-plugin", "css", "custom.css")))
                .IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_NoCrash_WhenPluginHasNoStaticDir()
    {
        var dir = CreateSiteWithPluginNoStatic();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateSiteWithPluginAssets()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-pluginasset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        var pluginDir = Path.Combine(dir, "plugins", "my-plugin");
        Directory.CreateDirectory(Path.Combine(pluginDir, "static", "css"));
        File.WriteAllText(Path.Combine(pluginDir, "plugin.yaml"),
            "name: My Plugin\nslots:\n  - head\n");
        File.WriteAllText(Path.Combine(pluginDir, "static", "css", "custom.css"),
            "/* custom */");

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html><body>{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        return dir;
    }

    private static string CreateSiteWithPluginNoStatic()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-pluginnostatic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        var pluginDir = Path.Combine(dir, "plugins", "lean-plugin");
        Directory.CreateDirectory(Path.Combine(pluginDir, "slots"));
        File.WriteAllText(Path.Combine(pluginDir, "plugin.yaml"),
            "name: Lean Plugin\nslots:\n  - after_content\n");
        File.WriteAllText(Path.Combine(pluginDir, "slots", "after_content.html"),
            "<div>lean</div>");

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html><body>{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        return dir;
    }

    private static ISiteBuilder CreateBuilder()
    {
        var markdownProcessor = new MarkdownProcessor();
        var contentReader = new ContentReader(markdownProcessor);
        var templateRenderer = new TemplateRenderer();
        var permalinkGenerator = new PermalinkGenerator();
        var configLoader = new SiteConfigLoader();
        var pluginLoader = new PluginLoader();
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader);
    }
}
