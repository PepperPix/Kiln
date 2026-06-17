namespace Kiln.Models;

/// <summary>
/// Site-wide configuration loaded from site.yaml at project root.
/// </summary>
public sealed class SiteConfiguration
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required Uri BaseUrl { get; init; }
    public string Language { get; init; } = "en";
    public string Theme { get; init; } = "default";
    public string ContentDir { get; init; } = "content";
    public string OutputDir { get; init; } = "_site";
    public string ThemesDir { get; init; } = "themes";
    public Dictionary<string, object> Extra { get; init; } = [];
}
