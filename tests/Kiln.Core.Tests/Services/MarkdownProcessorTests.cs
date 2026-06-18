namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class MarkdownProcessorTests
{
    private readonly MarkdownProcessor _processor = new();

    [Test]
    public async Task ToHtml_ConvertsBasicMarkdown()
    {
        var html = _processor.ToHtml("# Hello\n\nThis is **bold**.");

        await Assert.That(html).Contains("<h1 id=\"hello\">Hello</h1>");
        await Assert.That(html).Contains("<strong>bold</strong>");
    }

    [Test]
    public async Task ToHtml_ConvertsCodeBlocks()
    {
        var html = _processor.ToHtml("```csharp\nvar x = 1;\n```");

        await Assert.That(html).Contains("<code");
        await Assert.That(html).Contains("var x = 1;");
    }

    [Test]
    public async Task ToHtml_EmptyInput_ReturnsEmpty()
    {
        var html = _processor.ToHtml("");

        await Assert.That(html.Trim()).IsEqualTo("");
    }

    [Test]
    public async Task ToHtml_WithAssetBasePath_RewritesRelativeImageUrls()
    {
        var html = _processor.ToHtml("![Hero](hero.jpg)", "/assets/content/posts/my-post/");

        await Assert.That(html).Contains("src=\"/assets/content/posts/my-post/hero.jpg\"");
    }

    [Test]
    public async Task ToHtml_WithAssetBasePath_RewritesDotSlashPrefix()
    {
        var html = _processor.ToHtml("![Diagram](./diagram.svg)", "/assets/content/posts/my-post/");

        await Assert.That(html).Contains("src=\"/assets/content/posts/my-post/diagram.svg\"");
    }

    [Test]
    public async Task ToHtml_WithAssetBasePath_LeavesAbsoluteUrlsUnchanged()
    {
        var html = _processor.ToHtml("![External](https://example.com/img.png)", "/assets/content/posts/my-post/");

        await Assert.That(html).Contains("src=\"https://example.com/img.png\"");
    }

    [Test]
    public async Task ToHtml_WithAssetBasePath_LeavesRootRelativeUrlsUnchanged()
    {
        var html = _processor.ToHtml("![Logo](/images/logo.png)", "/assets/content/posts/my-post/");

        await Assert.That(html).Contains("src=\"/images/logo.png\"");
    }

    [Test]
    public async Task ToHtml_WithoutAssetBasePath_LeavesRelativeImageUrlsUnchanged()
    {
        var html = _processor.ToHtml("![Hero](hero.jpg)");

        await Assert.That(html).Contains("src=\"hero.jpg\"");
    }
}
