namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class MenuResolutionTests
{
    [Test]
    public async Task BuildAsync_MenuRef_CollectionIndex_Resolved()
    {
        var dir = CreateSiteWithMenus(
            """
            menus:
              main:
                - title: Blog
                  ref: posts/
            """);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Errors.Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_MenuRef_ItemSlug_Resolved()
    {
        var dir = CreateSiteWithMenus(
            """
            menus:
              main:
                - title: About
                  ref: pages/about
            """);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Errors.Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_MenuRef_UnknownCollection_ProducesError()
    {
        var dir = CreateSiteWithMenus(
            """
            menus:
              main:
                - title: Broken
                  ref: nonexistent/
            """);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Contains("nonexistent"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_MenuRef_UnknownItem_ProducesError()
    {
        var dir = CreateSiteWithMenus(
            """
            menus:
              main:
                - title: Broken
                  ref: pages/nonexistent
            """);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Contains("nonexistent"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_MenuUrl_UnknownPage_ProducesWarning()
    {
        var dir = CreateSiteWithMenus(
            """
            menus:
              main:
                - title: Home
                  url: /does-not-exist/
            """);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Warnings.Any(w => w.Contains("/does-not-exist/"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_MenuUrl_External_NoValidation()
    {
        var dir = CreateSiteWithMenus(
            """
            menus:
              main:
                - title: GitHub
                  url: https://github.com/example
                  external: true
            """);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Warnings.Any(w => w.Contains("github.com"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithMenus(string menusYaml)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-menu-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "pages"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $"""
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                paginate: 10
              pages:
                directory: content/pages
                permalink: /:slug/
            {menusYaml}
            """);

        File.WriteAllText(Path.Combine(dir, "content", "pages", "about.md"),
            """
            ---
            title: About
            ---
            About page
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");

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
