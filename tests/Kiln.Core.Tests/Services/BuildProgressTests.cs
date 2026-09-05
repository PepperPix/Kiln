namespace Kiln.Core.Tests.Services;

using Kiln.Abstractions;
using Kiln.Services;

public class BuildProgressTests
{
    [Test]
    public async Task BuildAsync_ReportsProgressWithFinalCompletedEqualsTotal()
    {
        var dir = CreateSiteWithMultiplePosts();

        try
        {
            var builder = CreateBuilder();
            var progress = new CaptureProgress();
            var result = await builder.BuildAsync(dir, includeDrafts: false, BuildEnvironment.Development, progress, default);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(progress.Values).IsNotEmpty();

            var last = progress.Values[^1];
            await Assert.That(last.Total).IsGreaterThan(0);
            await Assert.That(last.Completed).IsEqualTo(last.Total);
            await Assert.That(last.Phase).IsEqualTo("Rendering pages");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private const int PostCount = 3;

    private static string CreateSiteWithMultiplePosts()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-progress-{Guid.NewGuid():N}");
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
                permalink: /blog/:slug/
            """);

        for (var i = 1; i <= PostCount; i++)
        {
            File.WriteAllText(Path.Combine(dir, "content", "posts", $"post-{i}.md"),
                $"""
                ---
                title: Post {i}
                ---
                Content {i}
                """);
        }

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ page.content }}");
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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, [], new SkiaSharpImageOptimizer(), new AssetReferenceIndexBuilder());
    }

    private sealed class CaptureProgress : IProgress<BuildProgress>
    {
        public List<BuildProgress> Values { get; } = [];

        public void Report(BuildProgress value) => Values.Add(value);
    }
}
