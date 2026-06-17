namespace Kiln.Services;

using Markdig;

public sealed class MarkdownProcessor : IMarkdownProcessor
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownProcessor()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string ToHtml(string markdown)
    {
        return Markdown.ToHtml(markdown, _pipeline);
    }
}
