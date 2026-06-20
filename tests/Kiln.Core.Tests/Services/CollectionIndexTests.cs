namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class CollectionIndexTests
{
    [Test]
    public async Task BuildAsync_CollectionWithPaginate_IndexRendered()
    {
        var dir = CreateSiteWithPaginatedCollection();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "index.html"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_CollectionIndex_ContainsItemLink()
    {
        var dir = CreateSiteWithPaginatedCollection();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var indexHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "index.html"));
            await Assert.That(indexHtml).Contains("Hello World");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithPaginatedCollection()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-idx-{Guid.NewGuid():N}");
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
                permalink: /blog/:slug/
                paginate: 10
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ page.content }}");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "posts-index.html"),
            """
            {{ for item in paginator.items }}<a href="{{ item.url }}">{{ item.title }}</a>{{ end }}
            """);

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
