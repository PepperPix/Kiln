namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class MarkdownProcessorTests
{
    private readonly MarkdownProcessor _processor = new();

    [Fact]
    public void ToHtml_ConvertsBasicMarkdown()
    {
        var html = _processor.ToHtml("# Hello\n\nThis is **bold**.");

        Assert.Contains("<h1 id=\"hello\">Hello</h1>", html);
        Assert.Contains("<strong>bold</strong>", html);
    }

    [Fact]
    public void ToHtml_ConvertsCodeBlocks()
    {
        var html = _processor.ToHtml("```csharp\nvar x = 1;\n```");

        Assert.Contains("<code", html);
        Assert.Contains("var x = 1;", html);
    }

    [Fact]
    public void ToHtml_EmptyInput_ReturnsEmpty()
    {
        var html = _processor.ToHtml("");

        Assert.Equal("", html.Trim());
    }
}
