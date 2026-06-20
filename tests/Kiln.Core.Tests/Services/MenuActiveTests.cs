namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class MenuActiveTests
{
    [Test]
    public async Task Render_MenuItemActive_WhenCurrentPageMatches()
    {
        var dir = CreateSiteWithMenuAndActiveLayout();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var aboutHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "about", "index.html"));

            await Assert.That(aboutHtml).Contains("ABOUT_ACTIVE:true");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Render_MenuItemNotActive_WhenCurrentPageDiffers()
    {
        var dir = CreateSiteWithMenuAndActiveLayout();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            // On the about page, the home menu item should NOT be active
            var aboutHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "about", "index.html"));

            await Assert.That(aboutHtml).Contains("HOME_ACTIVE:false");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithMenuAndActiveLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-active-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "pages"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              pages:
                directory: content/pages
                permalink: /:slug/
            menus:
              main:
                - title: Home
                  url: /
                  external: true
                - title: About
                  ref: pages/about
            """);

        File.WriteAllText(Path.Combine(dir, "content", "pages", "about.md"),
            """
            ---
            title: About
            ---
            About page
            """);

        // Layout that renders active state from menus
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            """
            <html>
            HOME_ACTIVE:{{ menus.main[0].active }}
            ABOUT_ACTIVE:{{ menus.main[1].active }}
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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader);
    }
}
