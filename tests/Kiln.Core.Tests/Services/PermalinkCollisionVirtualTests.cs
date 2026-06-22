namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PermalinkCollisionVirtualTests
{
    [Test]
    public async Task BuildAsync_VirtualPageCollidesWithContentItem_ReportsError()
    {
        // A content item at /tags/ will collide with the taxonomy overview page
        var dir = CreateSiteWithCollisionAtTaxonomyOverview();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Contains("Permalink collision"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithCollisionAtTaxonomyOverview()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-vcoll-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "pages"));
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
                taxonomies:
                  - tags
              pages:
                directory: content/pages
                permalink: /:slug/
            taxonomies:
              tags:
                permalink: /tags/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "post.md"),
            """
            ---
            title: My Post
            tags:
              - dotnet
            ---
            Content
            """);

        // This page claims the /tags/ URL, which collides with the taxonomy overview
        File.WriteAllText(Path.Combine(dir, "content", "pages", "tags.md"),
            """
            ---
            title: Tags Page
            slug: tags
            ---
            Tags
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ page.content }}");

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
