namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class TemplateCacheTests
{
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly TemplateRenderer _renderer = new();

    [Test]
    public async Task RenderNotFound_UnchangedFile_ServesCachedParse()
    {
        var theme = CreateTheme("404.html", "AAAA", Stamp);
        try
        {
            var (site, shared) = CreateSite();
            var first = _renderer.RenderNotFound(shared, site, theme, []);

            // Same length and timestamp: a re-parse would pick up the new text, a cache hit keeps the old one.
            WriteLayout(theme, "404.html", "BBBB", Stamp);
            var second = _renderer.RenderNotFound(shared, site, theme, []);

            await Assert.That(first).IsEqualTo("AAAA");
            await Assert.That(second).IsEqualTo("AAAA");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    public async Task RenderNotFound_ChangedTimestamp_ReparsesFile()
    {
        var theme = CreateTheme("404.html", "AAAA", Stamp);
        try
        {
            var (site, shared) = CreateSite();
            _ = _renderer.RenderNotFound(shared, site, theme, []);

            WriteLayout(theme, "404.html", "BBBB", Stamp.AddSeconds(5));
            var second = _renderer.RenderNotFound(shared, site, theme, []);

            await Assert.That(second).IsEqualTo("BBBB");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    public async Task RenderNotFound_ChangedLength_ReparsesFile()
    {
        var theme = CreateTheme("404.html", "AAAA", Stamp);
        try
        {
            var (site, shared) = CreateSite();
            _ = _renderer.RenderNotFound(shared, site, theme, []);

            WriteLayout(theme, "404.html", "BBBBBB", Stamp);
            var second = _renderer.RenderNotFound(shared, site, theme, []);

            await Assert.That(second).IsEqualTo("BBBBBB");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    public async Task RenderNotFound_ParseError_IsNotCached()
    {
        const string broken = "{{ for }}";
        const string fixedText = "ok-ok-ok";
        var theme = CreateTheme("404.html", broken, Stamp);
        try
        {
            var (site, shared) = CreateSite();
            await Assert.That(() => _renderer.RenderNotFound(shared, site, theme, []))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(() => _renderer.RenderNotFound(shared, site, theme, []))
                .ThrowsExactly<InvalidOperationException>();

            // Same timestamp; only a cached failure would keep throwing.
            WriteLayout(theme, "404.html", fixedText, Stamp);
            var html = _renderer.RenderNotFound(shared, site, theme, []);

            await Assert.That(html).IsEqualTo(fixedText);
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    public async Task RenderNotFound_ParseErrorMessage_NamesLayoutPath()
    {
        var theme = CreateTheme("404.html", "{{ for }}", Stamp);
        try
        {
            var (site, shared) = CreateSite();
            await Assert.That(() => _renderer.RenderNotFound(shared, site, theme, []))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("Template errors in");
            await Assert.That(() => _renderer.RenderNotFound(shared, site, theme, []))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("404.html");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    public async Task Render_PartialIsCachedAndInvalidatedByChange()
    {
        var theme = CreateTheme("404.html", "[{{ include 'p' }}]", Stamp);
        try
        {
            WritePartial(theme, "p", "one", Stamp);
            var (site, shared) = CreateSite();

            var first = _renderer.RenderNotFound(shared, site, theme, []);
            WritePartial(theme, "p", "two", Stamp);
            var cached = _renderer.RenderNotFound(shared, site, theme, []);
            WritePartial(theme, "p", "three", Stamp.AddSeconds(1));
            var changed = _renderer.RenderNotFound(shared, site, theme, []);

            await Assert.That(first).IsEqualTo("[one]");
            await Assert.That(cached).IsEqualTo("[one]");
            await Assert.That(changed).IsEqualTo("[three]");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    public async Task Render_SameCachedTemplateInParallel_ProducesIndependentCorrectResults()
    {
        const string layout = "{{ for i in 1..200 }}{{ page.title }}-{{ i }};{{ end }}{{ include 'p' }}";
        var theme = CreateTheme("default.html", layout, Stamp);
        try
        {
            WritePartial(theme, "p", "<{{ page.title }}>", Stamp);
            var collection = new ContentGroup { Name = "posts", Permalink = "/blog/:slug/", Layout = "default" };
            var site = new SiteConfiguration
            {
                Title = "Test Site",
                BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
                Collections = new Dictionary<string, ContentGroup> { ["posts"] = collection }
            };
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            const int taskCount = 32;
            const int rounds = 20;
            var items = Enumerable.Range(0, taskCount).Select(n => CreateItem(collection, $"t{n}")).ToList();
            var expected = items.Select(item => _renderer.Render(item, shared, site, theme, [])).ToList();

            var results = await Task.WhenAll(items.Select(item => Task.Run(() =>
            {
                var outputs = new List<string>(rounds);
                for (var round = 0; round < rounds; round++)
                    outputs.Add(_renderer.Render(item, shared, site, theme, []));
                return outputs;
            })));

            for (var n = 0; n < taskCount; n++)
            {
                await Assert.That(expected[n]).Contains($"t{n}-200;<t{n}>");
                await Assert.That(results[n].All(o => o == expected[n])).IsTrue();
            }
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    private static (SiteConfiguration Site, SharedRenderContext Shared) CreateSite()
    {
        var site = new SiteConfiguration
        {
            Title = "Test Site",
            BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri
        };
        return (site, SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>()));
    }

    private static string CreateTheme(string layoutFile, string content, DateTime stamp)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-tplcache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "partials"));
        WriteLayout(dir, layoutFile, content, stamp);
        return dir;
    }

    private static void WriteLayout(string theme, string file, string content, DateTime stamp)
    {
        var path = Path.Combine(theme, "layouts", file);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, stamp);
    }

    private static void WritePartial(string theme, string name, string content, DateTime stamp)
    {
        var path = Path.Combine(theme, "partials", $"{name}.html");
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, stamp);
    }

    private static ContentItem CreateItem(ContentGroup collection, string slug) => new()
    {
        SourcePath = $"/test/content/{slug}.md",
        RelativePath = $"{slug}.md",
        Title = slug,
        Slug = slug,
        RawContent = slug,
        HtmlContent = $"<p>{slug}</p>",
        Url = new Uri($"/blog/{slug}/", UriKind.Relative),
        OutputPath = $"blog/{slug}/index.html",
        Collection = collection
    };
}
