namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class SiteBuilderTests
{
    [Test]
    public async Task BuildAsync_DetectsPermalinkCollision()
    {
        var tempDir = CreateSiteWithCollision();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Count).IsGreaterThan(0);
            await Assert.That(result.Errors[0]).Contains("Permalink collision");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static ISiteBuilder CreateBuilder()
    {
        var markdownProcessor = new MarkdownProcessor();
        var contentReader = new ContentReader(markdownProcessor);
        var templateRenderer = new TemplateRenderer();
        var permalinkGenerator = new PermalinkGenerator();
        var configLoader = new SiteConfigLoader();
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader);
    }

    private static string CreateSiteWithCollision()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /:slug/
            """);

        // Two files that will produce the same URL because they have the same slug
        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello.md"),
            """
            ---
            title: Hello One
            slug: hello
            ---
            Content one
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello2.md"),
            """
            ---
            title: Hello Two
            slug: hello
            ---
            Content two
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");

        return dir;
    }
}
