namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class TaxonomyExtractionTests
{
    [Test]
    public async Task BuildAsync_ExtractsTaxonomyTerms_CountCorrect()
    {
        var dir = CreateSiteWithTaggedPosts(["dotnet", "kiln"], ["dotnet"]);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            // dotnet tag used twice, kiln once — tags/dotnet/ and tags/kiln/ must exist
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "tags", "dotnet", "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "tags", "kiln", "index.html"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_TaxonomyOverviewPage_Generated()
    {
        var dir = CreateSiteWithTaggedPosts(["dotnet"], []);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "tags", "index.html"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_TaxonomyOverview_ContainsTerm()
    {
        var dir = CreateSiteWithTaggedPosts(["dotnet"], []);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var overviewHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "tags", "index.html"));
            await Assert.That(overviewHtml).Contains("dotnet");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_TermsSortedByCountDesc()
    {
        // dotnet appears twice, kiln once — dotnet should be first in taxonomy overview
        var dir = CreateSiteWithTaggedPosts(["dotnet", "kiln"], ["dotnet"]);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var overviewHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "tags", "index.html"));
            var dotnetPos = overviewHtml.IndexOf("dotnet", StringComparison.Ordinal);
            var kilnPos = overviewHtml.IndexOf("kiln", StringComparison.Ordinal);
            await Assert.That(dotnetPos).IsLessThan(kilnPos);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_SlugNormalisedForTermWithSpaces()
    {
        var dir = CreateSiteWithTaggedPosts(["Hello World"], []);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "tags", "hello-world", "index.html"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithTaggedPosts(
        IReadOnlyList<string> post1Tags, IReadOnlyList<string> post2Tags)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-tax-{Guid.NewGuid():N}");
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
                taxonomies:
                  - tags
            taxonomies:
              tags:
                permalink: /tags/:slug/
            """);

        var tagList1 = string.Join("\n", post1Tags.Select(t => $"  - {t}"));
        File.WriteAllText(Path.Combine(dir, "content", "posts", "post1.md"),
            $"""
            ---
            title: Post One
            tags:
            {tagList1}
            ---
            Content one
            """);

        if (post2Tags.Count > 0)
        {
            var tagList2 = string.Join("\n", post2Tags.Select(t => $"  - {t}"));
            File.WriteAllText(Path.Combine(dir, "content", "posts", "post2.md"),
                $"""
                ---
                title: Post Two
                tags:
                {tagList2}
                ---
                Content two
                """);
        }

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ page.content }}");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "taxonomy.html"),
            """
            <h1>{{ taxonomy.term }}</h1>
            {{ for item in taxonomy.items }}<a href="{{ item.url }}">{{ item.title }}</a>{{ end }}
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "taxonomy-index.html"),
            """
            {{ for term in taxonomy.terms }}<a href="{{ term.url }}">{{ term.name }}</a>{{ end }}
            """);

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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, []);
    }
}
