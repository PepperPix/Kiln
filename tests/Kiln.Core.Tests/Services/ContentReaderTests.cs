namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class ContentReaderTests
{
    private readonly ContentReader _reader = new(new MarkdownProcessor());

    [Test]
    public async Task ReadAll_ReturnsEmptyForNonexistentDirectory()
    {
        var result = _reader.ReadAll("/nonexistent/path", "_site");

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ReadAll_ParsesFrontMatterAndContent()
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
            var result = _reader.ReadAll(tempDir, "_site");

            await Assert.That(result).HasSingleItem();
            var item = result[0];
            await Assert.That(item.FrontMatter.Title).IsEqualTo("Test Post");
            await Assert.That(item.FrontMatter.Date).IsEqualTo(new DateTime(2026, 6, 17));
            await Assert.That(item.FrontMatter.Tags).Contains("dotnet");
            await Assert.That(item.FrontMatter.Tags).Contains("kiln");
            await Assert.That(item.HtmlContent).Contains("<strong>world</strong>");
            await Assert.That(item.OutputPath).IsEqualTo("test/index.html");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadAll_SkipsFilesWithoutFrontMatter()
    {
        var tempDir = CreateTempContent("no-frontmatter.md", "Just plain markdown.");

        try
        {
            var result = _reader.ReadAll(tempDir, "_site");
            await Assert.That(result).IsEmpty();
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
}
