namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

/// <summary>
/// Real (non-mocked) E2E smoke test for <see cref="IAssetReferenceIndexBuilder"/>: reads an
/// actual on-disk project (same shape as the one exercised via the real <c>kiln build</c> CLI for
/// this plan's manual E2E smoketest) through <see cref="ContentReader"/> and verifies the index
/// correctly lists every content item referencing a shared Site-library image.
/// </summary>
public class AssetReferenceIndexBuilderE2ETests
{
    private const int ExpectedItemCount = 3;

    [Test]
    public async Task Build_RealProjectWithMultiplePostsReferencingSameSiteLibraryImage_ListsAllReferencingItems()
    {
        var dir = CreateProject();

        try
        {
            var markdownProcessor = new MarkdownProcessor();
            var contentReader = new ContentReader(markdownProcessor);
            var collection = new ContentGroup { Name = "posts", Directory = "content/posts" };

            var items = contentReader.ReadCollection(collection, dir);
            await Assert.That(items.Count).IsEqualTo(ExpectedItemCount);

            var builder = new AssetReferenceIndexBuilder();
            var index = builder.Build(items);

            await Assert.That(index.ContainsKey("/assets/img/shared-lib.png")).IsTrue();
            var sharedRefs = index["/assets/img/shared-lib.png"];
            var sharedTitles = sharedRefs.Select(r => r.Title).OrderBy(t => t, StringComparer.Ordinal).ToList();
            await Assert.That(sharedTitles).IsEquivalentTo(["Post One", "Post Two"]);

            await Assert.That(index.ContainsKey("/assets/img/unrelated.png")).IsTrue();
            var unrelatedRefs = index["/assets/img/unrelated.png"];
            await Assert.That(unrelatedRefs.Count).IsEqualTo(1);
            await Assert.That(unrelatedRefs[0].Title).IsEqualTo("Post Three");
            await Assert.That(unrelatedRefs[0].CollectionName).IsEqualTo("posts");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-asset-index-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));

        File.WriteAllText(Path.Combine(dir, "content", "posts", "post-one.md"),
            """
            ---
            title: Post One
            date: 2026-01-01
            ---
            ![Shared Library Image](/assets/img/shared-lib.png)
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "post-two.md"),
            """
            ---
            title: Post Two
            date: 2026-01-02
            ---
            Some text, then the same shared image again:

            ![Shared Library Image Again](/assets/img/shared-lib.png)
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "post-three.md"),
            """
            ---
            title: Post Three
            date: 2026-01-03
            ---
            This post references a different image entirely:

            ![Unrelated Image](/assets/img/unrelated.png)
            """);

        return dir;
    }
}
