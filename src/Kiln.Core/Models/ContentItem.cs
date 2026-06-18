namespace Kiln.Models;

public sealed class ContentItem
{
    public string? Id { get; init; }
    public required string Title { get; init; }
    public DateTime? Date { get; init; }
    public bool Draft { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public string? Layout { get; init; }
    public int Weight { get; init; }
    public required string SourcePath { get; init; }
    public required string RelativePath { get; init; }
    public required string RawContent { get; init; }
    public required string HtmlContent { get; init; }
    public required Uri Url { get; set; }
    public required string OutputPath { get; set; }
    public required ContentGroup Collection { get; init; }
    public Dictionary<string, object> Extra { get; init; } = [];
    public Dictionary<string, object> Taxonomies { get; init; } = [];

    /// <summary>
    /// Path to the directory containing co-located assets (Page Bundle).
    /// Null when the item is a plain .md file.
    /// </summary>
    public string? AssetDirectory { get; init; }
}

