namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class SiteBuilderTests
{
    [Test]
    public async Task BuildAsync_DetectsPermalinkCollision()
    {
        var tempDir = CreateSiteWithCollision();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Count).IsGreaterThan(0);
            await Assert.That(result.Errors[0]).Contains("Permalink collision");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_CopiesThemeAssetsToAssetsSubdir()
    {
        var tempDir = CreateSiteWithThemeAsset();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "_site", "assets", "css", "style.css"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "_site", "css", "style.css"))).IsFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_CopiesPageBundleAssetsToAssetsContentDir()
    {
        var tempDir = CreateSiteWithPageBundle();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "_site", "assets", "content", "posts", "with-image", "hero.txt"))).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateSiteWithThemeAsset()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "static", "css"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello.md"),
            """
            ---
            title: Hello
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "static", "css", "style.css"),
            "body { color: red; }");

        return dir;
    }

    private static string CreateSiteWithPageBundle()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-bundle-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(dir, "content", "posts", "with-image");
        Directory.CreateDirectory(bundleDir);
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        File.WriteAllText(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Post With Image
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(bundleDir, "hero.txt"), "asset content");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
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

    private static string CreateSiteWithCollision()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /:slug/
            """);

        // Two files that will produce the same URL because they have the same slug
        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello.md"),
            """
            ---
            title: Hello One
            slug: hello
            ---
            Content one
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello2.md"),
            """
            ---
            title: Hello Two
            slug: hello
            ---
            Content two
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        return dir;
    }
}
