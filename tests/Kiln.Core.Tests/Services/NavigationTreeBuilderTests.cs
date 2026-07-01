namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class NavigationTreeBuilderTests
{
    [Test]
    public async Task Build_FlatItems_FlatTree()
    {
        var items = new List<ContentItem>
        {
            MakeItem("Hello", "/hello/", 0),
            MakeItem("World", "/world/", 1)
        };

        var result = NavigationTreeBuilder.Build(items);

        await Assert.That(result).ContainsKey("posts");
        var roots = result["posts"];
        const int expectedCount = 2;
        await Assert.That(roots).Count().IsEqualTo(expectedCount);
        await Assert.That(roots[0].Title).IsEqualTo("Hello");
        await Assert.That(roots[0].Url.OriginalString).IsEqualTo("/hello/");
        await Assert.That(roots[1].Title).IsEqualTo("World");
    }

    [Test]
    public async Task Build_NestedItems_CreatesSectionNodes()
    {
        var col = MakeCollection();
        var items = new List<ContentItem>
        {
            MakeItem("Install", "/guides/install/", 0, sectionPath: "guides", collection: col),
            MakeItem("Config", "/guides/advanced/config/", 1, sectionPath: "guides/advanced", collection: col)
        };

        var result = NavigationTreeBuilder.Build(items);

        await Assert.That(result).ContainsKey("posts");
        var roots = result["posts"];
        await Assert.That(roots).Count().IsEqualTo(1);
        await Assert.That(roots[0].Title).IsEqualTo("Guides");
        const int expectedChildCount = 2;
        await Assert.That(roots[0].Children).Count().IsEqualTo(expectedChildCount);

        var configSection = roots[0].Children[0];
        var install = roots[0].Children[1];
        await Assert.That(configSection.Title).IsEqualTo("Advanced");
        const int configChildCount = 1;
        await Assert.That(configSection.Children).Count().IsEqualTo(configChildCount);
        await Assert.That(configSection.Children[0].Title).IsEqualTo("Config");

        await Assert.That(install.Title).IsEqualTo("Install");
        await Assert.That(install.Children).IsEmpty();
    }

    [Test]
    public async Task Build_SiblingsSortedByWeightThenTitle()
    {
        var items = new List<ContentItem>
        {
            MakeItem("Z Item", "/z/", 1),
            MakeItem("A Item", "/a/", 0),
            MakeItem("B Item", "/b/", 0)
        };

        var result = NavigationTreeBuilder.Build(items);
        var roots = result["posts"];

        const int lastIdx = 2;
        await Assert.That(roots[0].Title).IsEqualTo("A Item");
        await Assert.That(roots[1].Title).IsEqualTo("B Item");
        await Assert.That(roots[lastIdx].Title).IsEqualTo("Z Item");
    }

    [Test]
    public async Task Humanize_ConvertsKebabCase()
    {
        var result = NavigationTreeBuilder.Humanize("getting-started");
        await Assert.That(result).IsEqualTo("Getting Started");
    }

    [Test]
    public async Task Humanize_ConvertsSnakeCase()
    {
        var result = NavigationTreeBuilder.Humanize("hello_world");
        await Assert.That(result).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Humanize_LeavesPlainWord()
    {
        var result = NavigationTreeBuilder.Humanize("Guides");
        await Assert.That(result).IsEqualTo("Guides");
    }

    [Test]
    public async Task Build_MultipleCollections_SeparateTrees()
    {
        var postsCol = new ContentGroup { Name = "posts" };
        var pagesCol = new ContentGroup { Name = "pages" };
        var items = new List<ContentItem>
        {
            MakeItem("Post1", "/post1/", 0, collection: postsCol),
            MakeItem("Page1", "/page1/", 0, collection: pagesCol)
        };

        var result = NavigationTreeBuilder.Build(items);

        await Assert.That(result).ContainsKey("posts");
        await Assert.That(result).ContainsKey("pages");
        await Assert.That(result["posts"]).Count().IsEqualTo(1);
        await Assert.That(result["pages"]).Count().IsEqualTo(1);
    }

    private static ContentItem MakeItem(string title, string url, int weight, string sectionPath = "", ContentGroup? collection = null)
    {
        var col = collection ?? MakeCollection();
        return new ContentItem
        {
            Title = title,
            Slug = url.Trim('/').Split('/').Last(),
            SectionPath = sectionPath,
            Weight = weight,
            SourcePath = "/test.md",
            RelativePath = "test.md",
            RawContent = "",
            HtmlContent = "",
            Url = new Uri(url, UriKind.Relative),
            OutputPath = url.Trim('/') + "/index.html",
            Collection = col
        };
    }

    private static ContentGroup MakeCollection(string name = "posts") =>
        new() { Name = name };
}
