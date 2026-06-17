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
}
