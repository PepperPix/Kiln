namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    [Test]
    public async Task Render_AppliesLayoutWithSiteAndPageData()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><title>{{ page.title }} \u2014 {{ site.title }}</title><body>{{ page.content }}</body></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("<title>Test Post — Test Site</title>");
            await Assert.That(result).Contains("<p>Hello</p>");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_FallsBackToDefaultLayout()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><title>{{ page.title }} — {{ site.title }}</title><body>{{ page.content }}</body></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection(layout: "nonexistent");
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            // Should fall back to default layout since "nonexistent.html" doesn't exist
            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("<title>Test Post — Test Site</title>");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_ThrowsForMissingLayoutAndNoDefault()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html></html>",
            layoutName: "other");
        var collection = CreateTestCollection(layout: "nonexistent");
        var item = CreateTestItem("<p>Hello</p>", collection);
        var site = CreateTestSite(collection);
        var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

        await Assert.That(() => _renderer.Render(item, shared, site, tempTheme, []))
            .ThrowsExactly<FileNotFoundException>();
    }

    [Test]
    public async Task Render_AssetUrlFunctionResolvesPath()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><head>{{ asset_url 'css/style.css' }}</head></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/assets/css/style.css");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_PageAssetUrlFunctionResolvesColocatedAsset()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html>{{ page_asset_url 'hero.jpg' }}</html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/assets/content/posts/test-post/hero.jpg");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    private static string CreateTempTheme(string layout, string layoutName)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-theme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "partials"));
        File.WriteAllText(Path.Combine(dir, "layouts", $"{layoutName}.html"), layout);
        return dir;
    }

    private static ContentGroup CreateTestCollection(string layout = "default") =>
        new() { Name = "posts", Permalink = "/blog/:slug/", Layout = layout };

    [Test]
    public async Task Render_BreadcrumbAncestors_RendersForFlatItem()
    {
        const string layout = """
            <html>
            {{ for a in page.ancestors }}{{ a.title }}|{{ a.url }},{{ end }}
            </html>
            """;
        var tempTheme = CreateTempTheme(layout, "default");

        try
        {
            var collection = new ContentGroup { Name = "posts", Permalink = "/blog/:slug/", Layout = "default" };
            var item = new ContentItem
            {
                SourcePath = "/test/post.md",
                RelativePath = "post.md",
                Title = "Hello",
                Slug = "hello",
                RawContent = "",
                HtmlContent = "<p>Hi</p>",
                SectionPath = "",
                Url = new Uri("/blog/hello/", UriKind.Relative),
                OutputPath = "blog/hello/index.html",
                Collection = collection
            };
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("Posts|/");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_BreadcrumbAncestors_RendersNestedSections()
    {
        const string layout = """
            <html>
            {{ for a in page.ancestors }}{{ a.title }}|{{ a.url }} {{ end }}
            </html>
            """;
        var tempTheme = CreateTempTheme(layout, "default");

        try
        {
            var collection = new ContentGroup { Name = "docs", Permalink = "/:slug/", Layout = "default" };
            var item = new ContentItem
            {
                SourcePath = "/test/guides/advanced/config.md",
                RelativePath = "guides/advanced/config.md",
                Title = "Config",
                Slug = "config",
                RawContent = "",
                HtmlContent = "<p>Config</p>",
                SectionPath = "guides/advanced",
                Url = new Uri("/guides/advanced/config/", UriKind.Relative),
                OutputPath = "guides/advanced/config/index.html",
                Collection = collection
            };
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("Docs|/");
            await Assert.That(result).Contains("Guides|/guides/");
            await Assert.That(result).Contains("Advanced|/guides/advanced/");
            await Assert.That(result).DoesNotContain("Config|");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    private static ContentItem CreateTestItem(string htmlContent, ContentGroup collection, string? layout = null) => new()
    {
        SourcePath = "/test/content/test.md",
        RelativePath = "test.md",
        Title = "Test Post",
        Date = new DateTime(2026, 6, 17),
        Slug = "test-post",
        Layout = layout,
        RawContent = "# Test",
        HtmlContent = htmlContent,
        Url = new Uri("/blog/test-post/", UriKind.Relative),
        OutputPath = "blog/test-post/index.html",
        Collection = collection
    };

    private static SiteConfiguration CreateTestSite(ContentGroup collection) => new()
    {
        Title = "Test Site",
        BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
        Collections = new Dictionary<string, ContentGroup> { ["posts"] = collection }
    };
}

