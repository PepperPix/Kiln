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

#pragma warning disable S109
            await Assert.That(config.Collections).Count().IsEqualTo(2);
#pragma warning restore S109
            await Assert.That(config.Collections.ContainsKey("posts")).IsTrue();
            await Assert.That(config.Collections["posts"].Directory).IsEqualTo("content/blog");
            await Assert.That(config.Collections["posts"].Permalink).IsEqualTo("/blog/:slug/");
            await Assert.That(config.Collections["posts"].Sort).IsEqualTo("date desc");
            await Assert.That(config.Collections["posts"].Feed).IsTrue();
#pragma warning disable S109
            await Assert.That(config.Collections["posts"].Paginate).IsEqualTo(5);
#pragma warning restore S109
            await Assert.That(config.Collections["posts"].Layout).IsEqualTo("post");

#pragma warning disable S109
            await Assert.That(config.Taxonomies).Count().IsEqualTo(1);
#pragma warning restore S109
            await Assert.That(config.Taxonomies["tags"].Permalink).IsEqualTo("/t/:slug/");
#pragma warning disable S109
            await Assert.That(config.Taxonomies["tags"].Paginate).IsEqualTo(15);
#pragma warning restore S109
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
        await File.WriteAllTextAsync(Path.Combine(dir, "site.yml"), """
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

    [Test]
    public async Task Load_HomePageAndCollectionSet_Throws()
    {
        var dir = CreateTempSite("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
            home:
              page: content/index.md
              collection: posts
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("home");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomeBlockEmpty_Throws()
    {
        var dir = CreateTempSite("""
            title: Test
            baseUrl: http://localhost
            home: {}
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("home");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomeCollectionUnknown_Throws()
    {
        var dir = CreateTempSite("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
            home:
              collection: pages
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("home.collection");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomePage_MapsToConfiguration()
    {
        var dir = CreateTempSite("""
            title: Test
            baseUrl: http://localhost
            home:
              page: content/index.md
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Home).IsNotNull();
            await Assert.That(config.Home!.Page).IsEqualTo("content/index.md");
            await Assert.That(config.Home.Collection).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomeCollection_MapsToConfiguration()
    {
        var dir = CreateTempSite("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
            home:
              collection: posts
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Home).IsNotNull();
            await Assert.That(config.Home!.Collection).IsEqualTo("posts");
            await Assert.That(config.Home.Page).IsNull();
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
#pragma warning disable S6966 // WriteAllTextAsync not applicable in non-async helper
        File.WriteAllText(Path.Combine(dir, "site.yaml"), yamlContent);
#pragma warning restore S6966
        return dir;
    }
}
