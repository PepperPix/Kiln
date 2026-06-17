namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// Parsed YAML front matter from a content file.
/// </summary>
public sealed class FrontMatter
{
    public required string Title { get; init; }
    public DateTime? Date { get; init; }
    public bool Draft { get; init; }
    public string? Layout { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public Collection<string> Tags { get; init; } = [];
    public Collection<string> Categories { get; init; } = [];
    public Dictionary<string, object> Extra { get; init; } = [];
}
