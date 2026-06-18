namespace Kiln.Models;

public sealed class SiteConfiguration
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required Uri BaseUrl { get; init; }
    public string Language { get; init; } = "en";
    public string Theme { get; init; } = "default";
    public string AssetPrefix { get; init; } = "/assets/";
    public string OutputDir { get; init; } = "_site";
    public string ThemesDir { get; init; } = "themes";
    public Dictionary<string, ContentGroup> Collections { get; init; } = [];
    public Dictionary<string, TaxonomyDefinition> Taxonomies { get; init; } = [];
    public Dictionary<string, Menu> Menus { get; init; } = [];
    public Dictionary<string, object> Plugins { get; init; } = [];
    public Dictionary<string, object> ThemeConfig { get; init; } = [];
    public Dictionary<string, object> Extra { get; init; } = [];
}
