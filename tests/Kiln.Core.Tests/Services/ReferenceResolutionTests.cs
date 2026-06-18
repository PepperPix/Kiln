namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class ReferenceResolutionTests
{
    [Test]
    public async Task BuildAsync_AuthorReference_Resolved()
    {
        var dir = CreateSiteWithAuthorReference();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var postHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));

            await Assert.That(postHtml).Contains("Marcel Kummerow");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_MissingReference_ProducesWarning()
    {
        var dir = CreateSiteWithMissingAuthorReference();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Warnings.Count).IsGreaterThan(0);
            await Assert.That(result.Warnings.Any(w => w.Contains("not found"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithAuthorReference()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-ref-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "authors"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                references:
                  author: authors
              authors:
                directory: content/authors
                permalink: /authors/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            author: marcel
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "content", "authors", "marcel.md"),
            """
            ---
            title: Marcel Kummerow
            ---
            About me
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ if page.author }}{{ page.author.title }}{{ end }}");

        return dir;
    }

    private static string CreateSiteWithMissingAuthorReference()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-ref-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "authors"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                references:
                  author: authors
              authors:
                directory: content/authors
                permalink: /authors/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            author: nobody
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ page.content }}");

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
