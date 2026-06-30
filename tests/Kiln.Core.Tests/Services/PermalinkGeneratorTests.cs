namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class PermalinkGeneratorTests
{
    private readonly PermalinkGenerator _generator = new();

    [Test]
    public async Task Generate_ReplacesSlug()
    {
        var collection = MakeCollection(permalink: "/blog/:slug/");
        var item = MakeItem(slug: "hello-world");

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/blog/hello-world/");
    }

    [Test]
    public async Task Generate_ReplacesCollectionName()
    {
        var collection = MakeCollection(permalink: "/:collection/:slug/", name: "articles");
        var item = MakeItem(slug: "my-post");

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/articles/my-post/");
    }

    [Test]
    public async Task Generate_ReplacesDateTokens()
    {
        var collection = MakeCollection(permalink: "/:year/:month/:day/:slug/");
        var item = MakeItem(slug: "post", date: new DateTime(2026, 6, 18));

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/2026/06/18/post/");
    }

    [Test]
    public async Task Generate_EnsuresLeadingAndTrailingSlash()
    {
        var collection = MakeCollection(permalink: ":slug");
        var item = MakeItem(slug: "page");

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/page/");
    }

    [Test]
    public async Task Generate_HonorsPermalinkOverrideFromFrontmatter()
    {
        var collection = MakeCollection(permalink: "/blog/:slug/");
        var item = MakeItem(slug: "something", extra: new Dictionary<string, object> { ["permalink_override"] = "/custom/path/" });

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/custom/path/");
    }

    [Test]
    public async Task Generate_FlatItem_UnchangedSlug()
    {
        var collection = MakeCollection(permalink: "/:slug/");
        var item = MakeItem(slug: "install");

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/install/");
    }

    [Test]
    public async Task Generate_NestedItem_PathBasedSlug()
    {
        var collection = MakeCollection(permalink: "/:slug/");
        var item = MakeItem(slug: "install", sectionPath: "guides/advanced");

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/guides/advanced/install/");
    }

    [Test]
    public async Task Generate_NestedItemWithPrefix()
    {
        var collection = MakeCollection(permalink: "/docs/:slug/");
        var item = MakeItem(slug: "install", sectionPath: "guides");

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/docs/guides/install/");
    }

    [Test]
    public async Task Generate_NestedItemWithPermalinkOverride_OverrideWins()
    {
        var collection = MakeCollection(permalink: "/:slug/");
        var item = MakeItem(slug: "install", sectionPath: "guides",
            extra: new Dictionary<string, object> { ["permalink_override"] = "/custom/path/" });

        var result = _generator.Generate(item, collection);

        await Assert.That(result.OriginalString).IsEqualTo("/custom/path/");
    }

    private static ContentGroup MakeCollection(string permalink, string name = "posts") =>
        new() { Name = name, Permalink = permalink };

    private static ContentItem MakeItem(string slug, DateTime? date = null, string sectionPath = "", Dictionary<string, object>? extra = null)
    {
        var collection = new ContentGroup { Name = "posts", Permalink = "/:slug/" };
        return new ContentItem
        {
            Title = "Test",
            Slug = slug,
            SectionPath = sectionPath,
            Date = date,
            SourcePath = "/test.md",
            RelativePath = "test.md",
            RawContent = "",
            HtmlContent = "",
            Url = new Uri("/", UriKind.Relative),
            OutputPath = "",
            Collection = collection,
            Extra = extra ?? []
        };
    }
}
