namespace Kiln.Core.Tests.Configuration;

using Kiln.Services;

public class SiteConfigLoaderTests
{
    private readonly SiteConfigLoader _loader = new();

    [Test]
    public async Task Load_ValidYaml_ParsesAllFields()
    {
        var dir = CreateTempSite("""
            title: My Site
            description: A test site
            baseUrl: https://example.com
            language: de
            theme: mytheme
            assetPrefix: /static/
            outputDir: dist
            themesDir: layouts

            collections:
              posts:
                directory: content/blog
                permalink: /blog/:slug/
                sort: date desc
                feed: true
                paginate: 5
                taxonomies:
                  - tags
                layout: post
              pages:
                directory: content/pages
                permalink: /:slug/
                sort: weight asc

            taxonomies:
              tags:
                permalink: /t/:slug/
                paginate: 15
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Title).IsEqualTo("My Site");
            await Assert.That(config.Description).IsEqualTo("A test site");
            await Assert.That(config.BaseUrl.ToString()).IsEqualTo("https://example.com/");
            await Assert.That(config.Language).IsEqualTo("de");
            await Assert.That(config.Theme).IsEqualTo("mytheme");
            await Assert.That(config.AssetPrefix).IsEqualTo("/static/");
            await Assert.That(config.OutputDir).IsEqualTo("dist");
            await Assert.That(config.ThemesDir).IsEqualTo("layouts");

            await Assert.That(config.Collections).HasCount().EqualTo(2);
            await Assert.That(config.Collections.ContainsKey("posts")).IsTrue();
            await Assert.That(config.Collections["posts"].Directory).IsEqualTo("content/blog");
            await Assert.That(config.Collections["posts"].Permalink).IsEqualTo("/blog/:slug/");
            await Assert.That(config.Collections["posts"].Sort).IsEqualTo("date desc");
            await Assert.That(config.Collections["posts"].Feed).IsTrue();
            await Assert.That(config.Collections["posts"].Paginate).IsEqualTo(5);
            await Assert.That(config.Collections["posts"].Layout).IsEqualTo("post");

            await Assert.That(config.Taxonomies).HasCount().EqualTo(1);
            await Assert.That(config.Taxonomies["tags"].Permalink).IsEqualTo("/t/:slug/");
            await Assert.That(config.Taxonomies["tags"].Paginate).IsEqualTo(15);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_MissingTitle_ThrowsWithClearMessage()
    {
        var dir = CreateTempSite("""
            baseUrl: https://example.com
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("missing required field: title");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_MissingBaseUrl_ThrowsWithClearMessage()
    {
        var dir = CreateTempSite("""
            title: Test
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("missing required field: baseUrl");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_NoSiteYaml_ThrowsFileNotFound()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_MinimalYaml_UsesDefaults()
    {
        var dir = CreateTempSite("""
            title: Minimal
            baseUrl: http://localhost:5555
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Language).IsEqualTo("en");
            await Assert.That(config.Theme).IsEqualTo("default");
            await Assert.That(config.AssetPrefix).IsEqualTo("/assets/");
            await Assert.That(config.OutputDir).IsEqualTo("_site");
            await Assert.That(config.ThemesDir).IsEqualTo("themes");
            await Assert.That(config.Collections).IsEmpty();
            await Assert.That(config.Taxonomies).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SiteYml_AlsoWorks()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yml"), """
            title: YML Test
            baseUrl: http://localhost
            """);

        try
        {
            var config = _loader.Load(dir);
            await Assert.That(config.Title).IsEqualTo("YML Test");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateTempSite(string yamlContent)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), yamlContent);
        return dir;
    }
}
