namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class ContentReaderTests
{
    private readonly ContentReader _reader = new(new MarkdownProcessor());

    [Test]
    public async Task ReadCollection_ReturnsEmptyForNonexistentDirectory()
    {
        var collection = MakeCollection("posts", "/nonexistent/path");
        var result = _reader.ReadCollection(collection, "/nonexistent");

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ReadCollection_ParsesFrontMatterAndContent()
    {
        var tempDir = CreateTempContent(
            "test.md",
            """
            ---
            title: Test Post
            date: 2026-06-17
            tags:
              - dotnet
              - kiln
            ---

            Hello **world**!
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            var item = result[0];
            await Assert.That(item.Title).IsEqualTo("Test Post");
            await Assert.That(item.Date).IsEqualTo(new DateTime(2026, 6, 17));
            await Assert.That(item.Taxonomies.ContainsKey("tags")).IsTrue();
            await Assert.That(item.HtmlContent).Contains("<strong>world</strong>");
            await Assert.That(item.Slug).IsEqualTo("test");
            await Assert.That(item.Collection.Name).IsEqualTo("posts");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_SkipsFilesWithoutFrontMatter()
    {
        var tempDir = CreateTempContent("no-frontmatter.md", "Just plain markdown.");

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);
            await Assert.That(result).IsEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_SortsDateDesc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "old.md"),
            """
            ---
            title: Old
            date: 2024-01-01
            ---
            content
            """).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "new.md"),
            """
            ---
            title: New
            date: 2026-01-01
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir, sort: "date desc");
            var result = _reader.ReadCollection(collection, tempDir);

            const int expectedCount = 2;
            await Assert.That(result.Count).IsEqualTo(expectedCount);
            await Assert.That(result[0].Title).IsEqualTo("New");
            await Assert.That(result[1].Title).IsEqualTo("Old");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempContent(string fileName, string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
        return dir;
    }

    private static ContentGroup MakeCollection(string name, string directory, string sort = "none") =>
        new() { Name = name, Directory = directory, Sort = sort, Taxonomies = ["tags", "categories"] };
}

