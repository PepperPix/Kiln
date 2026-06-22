namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class PaginatorTests
{
    [Test]
    public async Task BuildAsync_PaginatedCollection_Page2Exists()
    {
        var dir = CreateSiteWithManyPosts(15, pageSize: 10);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "page", "2", "index.html"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_25Items_3Pages()
    {
        var dir = CreateSiteWithManyPosts(25, pageSize: 10);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "page", "3", "index.html"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Page1HasNextUrl_NoNextOnLastPage()
    {
        var dir = CreateSiteWithManyPosts(11, pageSize: 10);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var page1Html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "index.html"));
            var page2Html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "page", "2", "index.html"));

            await Assert.That(page1Html).Contains("next_url");
            await Assert.That(page2Html).Contains("prev_url");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_CollectionNoPaginate_NoIndexPage()
    {
        var dir = CreateSiteWithManyPosts(5, pageSize: 0);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "index.html"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithManyPosts(int count, int pageSize)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-pag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        var paginateClause = pageSize > 0 ? $"paginate: {pageSize}" : "";

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $"""
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                sort: date desc
                {paginateClause}
            """);

        for (var i = 1; i <= count; i++)
        {
            File.WriteAllText(Path.Combine(dir, "content", "posts", $"post-{i:D2}.md"),
                $"""
                ---
                title: Post {i}
                date: 2026-01-{i:D2}
                ---
                Content {i}
                """);
        }

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ page.content }}");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "posts-index.html"),
            """
            page={{ paginator.page }} next_url={{ paginator.next_url }} prev_url={{ paginator.prev_url }}
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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, []);
    }
}
