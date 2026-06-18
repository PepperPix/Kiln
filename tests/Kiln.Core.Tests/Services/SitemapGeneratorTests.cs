namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class SitemapGeneratorTests
{
    [Test]
    public async Task BuildAsync_GeneratesSitemapXml()
    {
        var dir = CreateSiteForSitemap();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "sitemap.xml"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Sitemap_ContainsContentUrl()
    {
        var dir = CreateSiteForSitemap();

        try
        {
            var builder = CreateBuilder();
            await builder.BuildAsync(dir);

            var sitemap = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "sitemap.xml"));

            await Assert.That(sitemap).Contains("http://localhost:5555/blog/hello-world/");
            await Assert.That(sitemap).Contains("<urlset");
            await Assert.That(sitemap).Contains("<loc>");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Sitemap_ContainsCollectionIndexUrl()
    {
        var dir = CreateSiteForSitemap();

        try
        {
            var builder = CreateBuilder();
            await builder.BuildAsync(dir);

            var sitemap = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "sitemap.xml"));

            await Assert.That(sitemap).Contains("http://localhost:5555/blog/");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Sitemap_ExcludesDrafts()
    {
        var dir = CreateSiteForSitemap();

        try
        {
            var builder = CreateBuilder();
            await builder.BuildAsync(dir);

            var sitemap = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "sitemap.xml"));

            await Assert.That(sitemap).DoesNotContain("/draft-post/");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteForSitemap()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-sitemap-{Guid.NewGuid():N}");
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
            date: 2026-01-01
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "draft-post.md"),
            """
            ---
            title: Draft Post
            draft: true
            ---
            Draft content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "posts-index.html"),
            "{{ for item in paginator.items }}<a href='{{ item.url }}'>{{ item.title }}</a>{{ end }}");

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
