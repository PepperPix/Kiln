namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class HomepageEngineTests
{
    [Test]
    public async Task BuildAsync_HomePageMode_RendersRootIndexWithLimitFilter()
    {
        const int postCount = 6;
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-home-page-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                sort: title asc
            home:
              page: content/index.md
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "content", "index.md"),
            """
            ---
            title: Home
            ---
            Welcome home.
            """);

        for (var i = 1; i <= postCount; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "content", "posts", $"post-{i}.md"),
                $"""
                ---
                title: Post {i}
                ---
                Body {i}
                """);
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "home.html"),
            "<html>{{ page.content }}|{{ for p in collections.posts.items | limit 5 }}X{{ end }}</html>");
        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "404.html"), "NOT_FOUND");

        try
        {
            var result = await CreateBuilder().BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var homeHtmlPath = Path.Combine(dir, "_site", "index.html");
            await Assert.That(File.Exists(homeHtmlPath)).IsTrue();
            var homeHtml = await File.ReadAllTextAsync(homeHtmlPath);
            await Assert.That(homeHtml).Contains("Welcome home");
            await Assert.That(homeHtml).Contains("|XXXXX");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_HomeCollectionPromotion_RendersRootPaginationAndKeepsPostPermalinks()
    {
        const int postCount = 3;
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-home-collection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                paginate: 2
            home:
              collection: posts
            """);

        for (var i = 1; i <= postCount; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "content", "posts", $"post-{i}.md"),
                $"""
                ---
                title: Post {i}
                ---
                Body {i}
                """);
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "posts-index.html"),
            "<html>INDEX{{ for p in paginator.items }}|{{ p.slug }}{{ end }}</html>");
        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "404.html"), "NOT_FOUND");

        try
        {
            var result = await CreateBuilder().BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "page", "2", "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "post-1", "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "page", "2", "index.html"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_HomeCollectionWithoutPaginate_ReturnsBuildError()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-home-collection-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            home:
              collection: posts
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "content", "posts", "post-1.md"),
            """
            ---
            title: Post 1
            ---
            Body
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");

        try
        {
            var result = await CreateBuilder().BuildAsync(dir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(e => e.Contains("home.collection requires 'paginate'"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_AlwaysEmits404Html_UsingSharedGlobalsWithoutPage()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-404-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "content", "posts", "post-1.md"),
            """
            ---
            title: Post 1
            ---
            Body
            """);

        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>{{ site.title }}|{{ asset_url 'img/logo.svg' }}|{{ if page }}HAS_PAGE{{ else }}NO_PAGE{{ end }}</html>");

        try
        {
            var result = await CreateBuilder().BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var notFoundPath = Path.Combine(dir, "_site", "404.html");
            await Assert.That(File.Exists(notFoundPath)).IsTrue();
            var notFoundHtml = await File.ReadAllTextAsync(notFoundPath);
            await Assert.That(notFoundHtml).Contains("Test Site");
            await Assert.That(notFoundHtml).Contains("/assets/img/logo.svg");
            await Assert.That(notFoundHtml).Contains("NO_PAGE");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
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
