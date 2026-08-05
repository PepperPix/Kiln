namespace Kiln.Core.Tests.Configuration;

using Kiln.Models;
using Kiln.Services;

public class SiteConfigLoaderTests
{
    private readonly SiteConfigLoader _loader = new();

    [Test]
    public async Task Load_ValidYaml_ParsesAllFields()
    {
        var dir = await CreateTempSiteAsync("""
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

            await Assert.That(config.Collections).Count().IsEqualTo(2);
            await Assert.That(config.Collections.ContainsKey("posts")).IsTrue();
            await Assert.That(config.Collections["posts"].Directory).IsEqualTo(Path.Combine("content", "blog"));
            await Assert.That(config.Collections["posts"].Permalink).IsEqualTo("/blog/:slug/");
            await Assert.That(config.Collections["posts"].Sort).IsEqualTo("date desc");
            await Assert.That(config.Collections["posts"].Feed).IsTrue();
            await Assert.That(config.Collections["posts"].Paginate).IsEqualTo(5);
            await Assert.That(config.Collections["posts"].Layout).IsEqualTo("post");

            await Assert.That(config.Taxonomies).Count().IsEqualTo(1);
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
        var dir = await CreateTempSiteAsync("""
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
        var dir = await CreateTempSiteAsync("""
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
        var dir = await CreateTempSiteAsync("""
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
    public async Task Load_CollectionWithoutExplicitDirectory_UsesNativeSeparator()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost

            collections:
              posts:
                permalink: /:slug/
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Collections["posts"].Directory).IsEqualTo(Path.Combine("content", "posts"));
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
        var dir = await CreateTempSiteAsync("""
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
        var dir = await CreateTempSiteAsync("""
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
        var dir = await CreateTempSiteAsync("""
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
        var dir = await CreateTempSiteAsync("""
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
        var dir = await CreateTempSiteAsync("""
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

    [Test]
    public async Task Load_SearchSection_ParsesAllFields()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            search:
              enabled: true
              extended: true
              binaryPath: /usr/local/bin/pagefind
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.Enabled).IsTrue();
            await Assert.That(config.Search.Extended).IsTrue();
            await Assert.That(config.Search.BinaryPath).IsEqualTo("/usr/local/bin/pagefind");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SearchSectionAbsent_UsesDefaults()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.Enabled).IsFalse();
            await Assert.That(config.Search.Extended).IsFalse();
            await Assert.That(config.Search.BinaryPath).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SearchEnabledOnly_OtherFieldsDefault()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            search:
              enabled: true
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.Enabled).IsTrue();
            await Assert.That(config.Search.Extended).IsFalse();
            await Assert.That(config.Search.BinaryPath).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_ImagesSection_ParsesAllFields()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            images:
              enabled: false
              maxWidth: 1200
              quality: 70
              webp: true
              exclude:
                - "static/hero-*.png"
                - "static/logos/**"
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Images.Enabled).IsFalse();
            await Assert.That(config.Images.MaxWidth).IsEqualTo(1200);
            await Assert.That(config.Images.Quality).IsEqualTo(70);
            await Assert.That(config.Images.Webp).IsTrue();
            await Assert.That(config.Images.Exclude).IsEquivalentTo(["static/hero-*.png", "static/logos/**"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_ImagesSectionAbsent_UsesDefaults()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Images.Enabled).IsTrue();
            await Assert.That(config.Images.MaxWidth).IsEqualTo(2000);
            await Assert.That(config.Images.Quality).IsEqualTo(82);
            await Assert.That(config.Images.Webp).IsFalse();
            await Assert.That(config.Images.Exclude).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_CollectionTeaserWords_OverridesDefault()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                teaserWords: 30
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Collections["posts"].TeaserWords).IsEqualTo(30);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_CollectionTeaserWordsAbsent_UsesDefault()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                permalink: /:slug/
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Collections["posts"].TeaserWords).IsEqualTo(ContentGroup.DefaultTeaserWords);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static async Task<string> CreateTempSiteAsync(string yamlContent)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"), yamlContent);
        return dir;
    }
}
