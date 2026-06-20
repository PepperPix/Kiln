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
        Directory.CreateDirectory(tempDir);        await File.WriteAllTextAsync(Path.Combine(tempDir, "old.md"),
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

    [Test]
    public async Task ReadCollection_DetectsPageBundle()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(tempDir, "my-post");
        Directory.CreateDirectory(bundleDir);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Bundle Post
            date: 2026-06-18
            ---

            Content with ![hero](hero.txt)
            """).ConfigureAwait(false);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "hero.txt"), "asset").ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            var item = result[0];
            await Assert.That(item.Title).IsEqualTo("Bundle Post");
            await Assert.That(item.Slug).IsEqualTo("my-post");
            await Assert.That(item.AssetDirectory).IsNotNull();
            await Assert.That(item.AssetDirectory).IsEqualTo(bundleDir);
            await Assert.That(item.HtmlContent).Contains("/assets/content/posts/my-post/hero.txt");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_IgnoresSubdirWithoutIndexMd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var subDir = Path.Combine(tempDir, "not-a-bundle");
        Directory.CreateDirectory(subDir);

        await File.WriteAllTextAsync(Path.Combine(subDir, "post.md"),
            """
            ---
            title: Not a bundle
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            // post.md is inside a subdir without index.md — should be ignored
            await Assert.That(result).IsEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadSingleFile_ParsesStandaloneFile()
    {
        var tempDir = CreateTempContent(
            "index.md",
            """
            ---
            title: Home
            ---
            Welcome home.
            """);

        try
        {
            var collection = MakeCollection("home", tempDir);
            var item = _reader.ReadSingleFile(Path.Combine(tempDir, "index.md"), collection);

            await Assert.That(item.Title).IsEqualTo("Home");
            await Assert.That(item.Slug).IsEqualTo("index");
            await Assert.That(item.Collection.Name).IsEqualTo("home");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadSingleFile_MissingFile_ThrowsFileNotFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var collection = MakeCollection("home", tempDir);
            await Assert.That(() => _reader.ReadSingleFile(Path.Combine(tempDir, "missing.md"), collection))
                .ThrowsExactly<FileNotFoundException>();
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

