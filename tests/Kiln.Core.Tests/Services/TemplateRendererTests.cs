namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    [Fact]
    public void Render_AppliesLayoutWithSiteAndPageData()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><title>{{ page.title }} — {{ site.title }}</title><body>{{ page.content }}</body></html>");

        try
        {
            var item = CreateTestItem("<p>Hello</p>");
            var site = CreateTestSite();

            var result = _renderer.Render(item, site, tempTheme);

            Assert.Contains("<title>Test Post — Test Site</title>", result);
            Assert.Contains("<p>Hello</p>", result);
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Fact]
    public void Render_ThrowsForMissingLayout()
    {
        var tempTheme = CreateTempTheme(layout: "");
        var missingLayoutItem = CreateTestItem("<p>Hello</p>", layout: "nonexistent");
        File.Delete(Path.Combine(tempTheme, "layouts", "nonexistent.html"));

        try
        {
            Assert.Throws<FileNotFoundException>(() =>
                _renderer.Render(missingLayoutItem, CreateTestSite(), tempTheme));
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    private static string CreateTempTheme(string layout)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-theme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "partials"));
        File.WriteAllText(Path.Combine(dir, "layouts", "default.html"), layout);
        return dir;
    }

    private static ContentItem CreateTestItem(string htmlContent, string layout = "default") => new()
    {
        SourcePath = "/test/content/test.md",
        RelativePath = "test.md",
        FrontMatter = new FrontMatter
        {
            Title = "Test Post",
            Date = new DateTime(2026, 6, 17),
            Layout = layout
        },
        RawContent = "# Test",
        HtmlContent = htmlContent,
        OutputPath = "test/index.html"
    };

    private static SiteConfiguration CreateTestSite() => new()
    {
        Title = "Test Site",
        BaseUrl = new Uri("http://localhost:5555")
    };
}
