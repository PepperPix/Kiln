namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class LimitFilterTests
{
    private readonly TemplateRenderer _renderer = new();

    [Test]
    public async Task Render_LimitFilter_HandlesNormalAndEdgeCases()
    {
        const string layout = "AB{{ for p in page.extra.missing | limit 5 }}X{{ end }}"
            + "|CD{{ for p in collections.posts.items | limit 0 }}X{{ end }}"
            + "|EF{{ for p in collections.posts.items | limit 5 }}X{{ end }}"
            + "|GH{{ for p in collections.posts.items | limit 1 }}X{{ end }}";
        var tempTheme = CreateTempTheme(layout);

        try
        {
            var collection = new ContentGroup { Name = "posts", Permalink = "/blog/:slug/", Layout = "default" };
            var first = CreateItem(collection, "first", "First", "/blog/first/");
            var second = CreateItem(collection, "second", "Second", "/blog/second/");
            collection.Items.Add(first);
            collection.Items.Add(second);

            var site = new SiteConfiguration
            {
                Title = "Test Site",
                BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
                Collections = new Dictionary<string, ContentGroup> { ["posts"] = collection }
            };

            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var html = _renderer.Render(first, shared, site, tempTheme, []);

            await Assert.That(html).IsEqualTo("AB|CD|EFXX|GHX");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    private static string CreateTempTheme(string layout)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "partials"));
        File.WriteAllText(Path.Combine(dir, "layouts", "default.html"), layout);
        return dir;
    }

    private static ContentItem CreateItem(ContentGroup collection, string slug, string title, string url) => new()
    {
        SourcePath = $"/test/content/{slug}.md",
        RelativePath = $"{slug}.md",
        Title = title,
        Slug = slug,
        RawContent = title,
        HtmlContent = $"<p>{title}</p>",
        Url = new Uri(url, UriKind.Relative),
        OutputPath = $"blog/{slug}/index.html",
        Collection = collection
    };
}
