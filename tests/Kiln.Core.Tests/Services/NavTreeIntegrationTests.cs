namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class NavTreeIntegrationTests
{
    [Test]
    public async Task Build_NavTreeMarkers_CorrectOnNestedPage()
    {
        var dir = CreateSiteWithNavLayout();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var configHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "guides", "advanced", "config", "index.html"));

            // config page: itself should be active in the tree
            await Assert.That(configHtml).Contains("NODE_TITLE:Config");
            await Assert.That(configHtml).Contains("NODE_ACTIVE:true");
            await Assert.That(configHtml).Contains("NODE_URL:/guides/advanced/config/");

            // advanced section should be ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Advanced");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:true");

            // guides section should be ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Guides");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:true");

            // top page should NOT be active or ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Top");
            await Assert.That(configHtml).Contains("NODE_ACTIVE:false");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:false");

            // getting started should NOT be active or ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Getting Started");
            await Assert.That(configHtml).Contains("NODE_ACTIVE:false");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:false");

            // Breadcrumbs
            await Assert.That(configHtml).Contains("BRE_ANCESTORS:Docs|/ ");
            await Assert.That(configHtml).Contains("Guides|/guides/ ");
            await Assert.That(configHtml).Contains("Advanced|/guides/advanced/ ");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Build_NavTreeMarkers_CorrectOnTopPage()
    {
        var dir = CreateSiteWithNavLayout();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var topHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "top", "index.html"));

            // top page: itself should be active
            await Assert.That(topHtml).Contains("NODE_TITLE:Top");
            await Assert.That(topHtml).Contains("NODE_ACTIVE:true");

            // guides section should NOT be ancestor (top page is at /top/, not under /guides/)
            await Assert.That(topHtml).Contains("NODE_TITLE:Guides");
            await Assert.That(topHtml).Contains("NODE_ANCESTOR:false");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithNavLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-nav-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "guides", "advanced"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              docs:
                directory: content
                permalink: /:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "top.md"),
            """
            ---
            title: Top
            ---
            top content
            """);

        File.WriteAllText(Path.Combine(dir, "content", "guides", "getting-started.md"),
            """
            ---
            title: Getting Started
            ---
            guide content
            """);

        File.WriteAllText(Path.Combine(dir, "content", "guides", "advanced", "config.md"),
            """
            ---
            title: Config
            ---
            deep content
            """);

        // Layout that renders navtree + breadcrumbs using recursion
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            """
            <html>
            {{ for node in navtree.docs }}
            NODE_TITLE:{{ node.title }}
            NODE_URL:{{ node.url }}
            NODE_ACTIVE:{{ node.is_active }}
            NODE_ANCESTOR:{{ node.is_ancestor }}
            {{ for child in node.children }}
            NODE_TITLE:{{ child.title }}
            NODE_URL:{{ child.url }}
            NODE_ACTIVE:{{ child.is_active }}
            NODE_ANCESTOR:{{ child.is_ancestor }}
            {{ for grandchild in child.children }}
            NODE_TITLE:{{ grandchild.title }}
            NODE_URL:{{ grandchild.url }}
            NODE_ACTIVE:{{ grandchild.is_active }}
            NODE_ANCESTOR:{{ grandchild.is_ancestor }}
            {{ end }}
            {{ end }}
            {{ end }}
            BRE_ANCESTORS:{{ for a in page.ancestors }}{{ a.title }}|{{ a.url }} {{ end }}
            {{ page.content }}
            </html>
            """);
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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, [], new SkiaSharpImageOptimizer());
    }
}
