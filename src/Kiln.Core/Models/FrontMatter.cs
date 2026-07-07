namespace Kiln.Models;

using YamlDotNet.Serialization;

public sealed class FrontMatter
{
    public string? Id { get; init; }
    public required string Title { get; init; }
    public DateTime? Date { get; init; }
    public bool Draft { get; init; }
    public string? Layout { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    [YamlMember(Alias = "url")]
    public string? PermalinkOverride { get; init; }
    public int Weight { get; init; }
    public Dictionary<string, object> Extra { get; init; } = [];
}
