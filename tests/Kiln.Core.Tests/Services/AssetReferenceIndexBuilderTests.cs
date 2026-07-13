namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class AssetReferenceIndexBuilderTests
{
    private const int TwoReferencingItems = 2;

    private readonly AssetReferenceIndexBuilder _builder = new();

    [Test]
    public async Task Build_MultipleItemsReferenceSameImage_ListsAllReferencingItems()
    {
        var itemOne = MakeItem("First Post", "/content/posts/first.md", "posts",
            """<p><img src="/assets/img/shared.png" alt=""></p>""");
        var itemTwo = MakeItem("Second Post", "/content/posts/second.md", "posts",
            """<p><img src="/assets/img/shared.png" alt=""></p>""");

        var index = _builder.Build([itemOne, itemTwo]);

        await Assert.That(index.ContainsKey("/assets/img/shared.png")).IsTrue();
        var refs = index["/assets/img/shared.png"];
        await Assert.That(refs.Count).IsEqualTo(TwoReferencingItems);
        await Assert.That(refs.Select(r => r.Title)).Contains("First Post");
        await Assert.That(refs.Select(r => r.Title)).Contains("Second Post");
        await Assert.That(refs.Select(r => r.SourcePath)).Contains("/content/posts/first.md");
        await Assert.That(refs.Select(r => r.SourcePath)).Contains("/content/posts/second.md");
        await Assert.That(refs.All(r => r.CollectionName == "posts")).IsTrue();
    }

    [Test]
    public async Task Build_ImageOptimizationFalseItem_ExcludedFromIndex()
    {
        var item = MakeItem("Opted Out", "/content/posts/opted-out.md", "posts",
            """<p><img src="/assets/img/opted-out.png" alt=""></p>""", imageOptimization: false);

        var index = _builder.Build([item]);

        // Matches the pre-extraction CollectReferencedImageWebPaths behavior: items with the
        // ImageOptimization opt-out are skipped entirely, not merely excluded from optimization.
        await Assert.That(index.ContainsKey("/assets/img/opted-out.png")).IsFalse();
    }

    [Test]
    public async Task Build_SameItemMultipleImgTagsSamePath_CollapsesToSingleRef()
    {
        var item = MakeItem("Repeated", "/content/posts/repeated.md", "posts",
            """<p><img src="/assets/img/shared.png"><img src="/assets/img/shared.png"></p>""");

        var index = _builder.Build([item]);

        await Assert.That(index["/assets/img/shared.png"].Count).IsEqualTo(1);
    }

    [Test]
    public async Task Build_SrcWithoutAssetsPrefix_Ignored()
    {
        var item = MakeItem("External", "/content/posts/external.md", "posts",
            """<p><img src="https://example.com/photo.png" alt=""></p>""");

        var index = _builder.Build([item]);

        await Assert.That(index.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Build_OnlyPassedInItems_NeverIntroducesUnrelatedReferences()
    {
        // Regression guard for the Content-Pages-only scope: SiteBuilder only ever passes
        // IContentReader-read ContentItems into Build (never Theme/Plugin static assets, which
        // aren't ContentItems at all) — so the index can never contain anything beyond what was
        // explicitly handed in.
        var item = MakeItem("Only Post", "/content/posts/only.md", "posts",
            """<p><img src="/assets/img/only.png" alt=""></p>""");

        var index = _builder.Build([item]);

        await Assert.That(index.Count).IsEqualTo(1);
        var refs = index["/assets/img/only.png"];
        await Assert.That(refs.Count).IsEqualTo(1);
        await Assert.That(refs[0]).IsEqualTo(new ContentItemRef("Only Post", "/content/posts/only.md", "posts"));
    }

    [Test]
    public async Task Build_NullItems_ThrowsArgumentNullException()
    {
        await Assert.That(() => _builder.Build(null!)).ThrowsExactly<ArgumentNullException>();
    }

    private static ContentItem MakeItem(string title, string sourcePath, string collectionName, string htmlContent, bool imageOptimization = true)
    {
        var collection = new ContentGroup { Name = collectionName };
        return new ContentItem
        {
            Title = title,
            Slug = "slug",
            SourcePath = sourcePath,
            RelativePath = sourcePath,
            RawContent = "",
            HtmlContent = htmlContent,
            Url = new Uri("/", UriKind.Relative),
            OutputPath = "",
            Collection = collection,
            ImageOptimization = imageOptimization,
        };
    }
}
