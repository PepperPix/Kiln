namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class NextPrevTests
{
    [Test]
    public async Task BuildAsync_NextPrev_SetForMiddleItem()
    {
        var dir = CreateSiteWithThreePosts();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var middleHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "blog", "post-02", "index.html"));

            await Assert.That(middleHtml).Contains("prev:Post 1");
            await Assert.That(middleHtml).Contains("next:Post 3");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_FirstItem_NoPrev()
    {
        var dir = CreateSiteWithThreePosts();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var firstHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "blog", "post-01", "index.html"));

            await Assert.That(firstHtml).DoesNotContain("prev:");
            await Assert.That(firstHtml).Contains("next:Post 2");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_LastItem_NoNext()
    {
        var dir = CreateSiteWithThreePosts();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var lastHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "blog", "post-03", "index.html"));

            await Assert.That(lastHtml).Contains("prev:Post 2");
            await Assert.That(lastHtml).DoesNotContain("next:");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithThreePosts()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-nextprev-{Guid.NewGuid():N}");
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
                sort: date asc
            """);

#pragma warning disable S109
        for (var i = 1; i <= 3; i++)
#pragma warning restore S109
        {
            File.WriteAllText(Path.Combine(dir, "content", "posts", $"post-0{i}.md"),
                $"""
                ---
                title: Post {i}
                date: 2026-01-0{i}
                ---
                Content {i}
                """);
        }

        // Template exposes prev/next title inline for easy assertion
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ if page.prev }}prev:{{ page.prev.title }}{{ end }}{{ if page.next }}next:{{ page.next.title }}{{ end }}");
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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, []);
    }
}
