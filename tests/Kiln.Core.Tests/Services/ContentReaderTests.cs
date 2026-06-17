namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class ContentReaderTests
{
    private readonly ContentReader _reader = new(new MarkdownProcessor());

    [Fact]
    public void ReadAll_ReturnsEmptyForNonexistentDirectory()
    {
        var result = _reader.ReadAll("/nonexistent/path", "_site");

        Assert.Empty(result);
    }

    [Fact]
    public void ReadAll_ParsesFrontMatterAndContent()
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

            Assert.Single(result);
            var item = result[0];
            Assert.Equal("Test Post", item.FrontMatter.Title);
            Assert.Equal(new DateTime(2026, 6, 17), item.FrontMatter.Date);
            Assert.Equal(["dotnet", "kiln"], item.FrontMatter.Tags);
            Assert.Contains("<strong>world</strong>", item.HtmlContent);
            Assert.Equal("test/index.html", item.OutputPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReadAll_SkipsFilesWithoutFrontMatter()
    {
        var tempDir = CreateTempContent("no-frontmatter.md", "Just plain markdown.");

        try
        {
            var result = _reader.ReadAll(tempDir, "_site");
            Assert.Empty(result);
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
