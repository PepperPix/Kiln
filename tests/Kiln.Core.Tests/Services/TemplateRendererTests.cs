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

            var result = _renderer.Render(item, site, tempTheme);

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

            // Should fall back to default layout since "nonexistent.html" doesn't exist
            var result = _renderer.Render(item, site, tempTheme);

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

        await Assert.That(() => _renderer.Render(item, CreateTestSite(collection), tempTheme))
            .ThrowsExactly<FileNotFoundException>();
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

