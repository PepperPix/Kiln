namespace Kiln.Models;

public sealed class SiteConfiguration
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required Uri BaseUrl { get; init; }
    public string BasePath => CalculateBasePath(BaseUrl);
    // Scheme+host only, without the base path — item URLs already carry the base path prefix,
    // so absolute-URL builders (sitemap/feed) must combine Origin with them, not the full BaseUrl.
    public string Origin => $"{BaseUrl.Scheme}://{BaseUrl.Authority}";
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
    public HomeConfiguration? Home { get; init; }
    public BuildOptions Build { get; init; } = new BuildOptions();
    public AssetsOptions Assets { get; init; } = new AssetsOptions();
    public SearchOptions Search { get; init; } = new SearchOptions();

    public SiteConfiguration WithBaseUrl(Uri baseUrl)
    {
        return new SiteConfiguration
        {
            Title = Title,
            Description = Description,
            BaseUrl = baseUrl,
            Language = Language,
            Theme = Theme,
            AssetPrefix = AssetPrefix,
            OutputDir = OutputDir,
            ThemesDir = ThemesDir,
            Collections = Collections,
            Taxonomies = Taxonomies,
            Menus = Menus,
            Plugins = Plugins,
            ThemeConfig = ThemeConfig,
            Extra = Extra,
            Home = Home,
            Build = Build,
            Assets = Assets,
            Search = Search,
        };
    }

    public static string CalculateBasePath(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        var path = baseUrl.AbsolutePath;
        return string.IsNullOrWhiteSpace(path) || string.Equals(path, "/", StringComparison.Ordinal)
            ? string.Empty
            : path.TrimEnd('/');
    }

    public static string ApplyBasePath(string basePath, Uri? url)
    {
        if (url is null)
            return string.Empty;

        var trimmed = url.OriginalString.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (IsExternalReference(trimmed))
            return trimmed;

        var normalized = trimmed.StartsWith('/') ? trimmed : $"/{trimmed.TrimStart('/')}";
        if (string.IsNullOrEmpty(basePath))
            return normalized;

        if (normalized.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return normalized;

        return $"{basePath.TrimEnd('/')}{normalized}";
    }

    public static string RemoveBasePath(Uri? url, string basePath)
    {
        if (url is null)
            return string.Empty;

        var trimmed = url.OriginalString.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (string.IsNullOrEmpty(basePath) || !trimmed.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return trimmed.TrimStart('/');

        var withoutPrefix = trimmed[basePath.Length..];
        return withoutPrefix.TrimStart('/');
    }

    private static bool IsExternalReference(string value)
    {
        if (value.Length > 0 && (value[0] == '#' || value[0] == '?'))
            return true;

        foreach (var prefix in new[]
                 {
                     "http://",
                     "https://",
                     "mailto:",
                     "tel:",
                     "data:",
                     "javascript:"
                 })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
