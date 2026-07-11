namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class SearchUiIntegrationTests
{
    [Test]
    public async Task Build_SearchEnabled_RendersSearchUi()
    {
        var dir = CreateSiteWithSearch(enabled: true);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var indexHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "index", "index.html"));

            await Assert.That(indexHtml).Contains("id=\"search\"");
            await Assert.That(indexHtml).Contains("pagefind-ui.js");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Build_SearchDisabled_OmitsSearchUi()
    {
        var dir = CreateSiteWithSearch(enabled: false);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var indexHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "index", "index.html"));

            await Assert.That(indexHtml).DoesNotContain("pagefind-ui.js");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateSiteWithSearch(bool enabled)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "pages"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        var searchYaml = enabled ? "  enabled: true\n" : "";

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $$"""
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              pages:
                directory: content/pages
                permalink: /:slug/
            search:
            {{searchYaml}}
            """);

        File.WriteAllText(Path.Combine(dir, "content", "pages", "index.md"),
            """
            ---
            title: Home
            ---
            Home page content
            """);

        // Layout that includes the search partial
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            """
            <html>
            {{ include "search" }}
            {{ page.content }}
            </html>
            """);
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        // Search partial (same as real theme)
        File.WriteAllText(Path.Combine(dir, "themes", "default", "partials", "search.html"),
            """
            {{ if site.search.enabled }}
            <link href="/pagefind/pagefind-ui.css" rel="stylesheet" />
            <div id="search" class="kiln-search"></div>
            <script src="/pagefind/pagefind-ui.js"></script>
            <script>
              window.addEventListener("DOMContentLoaded", function () {
                new PagefindUI({ element: "#search", showSubResults: true });
              });
            </script>
            {{ end }}
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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, [], new SkiaSharpImageOptimizer());
    }
}
