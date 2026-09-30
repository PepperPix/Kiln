namespace Kiln.Models;

/// <summary>
/// The configuration of a site, loaded from <c>site.yaml</c>.
/// </summary>
public sealed class SiteConfiguration
{
    /// <summary>Gets the title of the site.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the description of the site.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the base URL of the site, read from <c>baseUrl</c>.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Gets the path portion of <see cref="BaseUrl"/> without a trailing slash. It is empty when the site is served from the root.</summary>
    public string BasePath => CalculateBasePath(BaseUrl);

    // Scheme+host only, without the base path — item URLs already carry the base path prefix,
    // so absolute-URL builders (sitemap/feed) must combine Origin with them, not the full BaseUrl.
    /// <summary>Gets the scheme and authority of <see cref="BaseUrl"/>, without the base path.</summary>
    public string Origin => $"{BaseUrl.Scheme}://{BaseUrl.Authority}";

    /// <summary>Gets the language code of the site. The default is <c>en</c>.</summary>
    public string Language { get; init; } = "en";

    /// <summary>Gets the name of the theme, which is the name of a directory below <see cref="ThemesDir"/>. The default is <c>default</c>.</summary>
    public string Theme { get; init; } = "default";

    /// <summary>Gets the URL prefix of the asset directory in the output. The default is <c>/assets/</c>.</summary>
    public string AssetPrefix { get; init; } = "/assets/";

    /// <summary>Gets the output directory, relative to the project directory. The default is <c>_site</c>.</summary>
    public string OutputDir { get; init; } = "_site";

    /// <summary>Gets the directory containing the themes, relative to the project directory. The default is <c>themes</c>.</summary>
    public string ThemesDir { get; init; } = "themes";

    /// <summary>Gets the collections of the site, keyed by collection name.</summary>
    public Dictionary<string, ContentGroup> Collections { get; init; } = [];

    /// <summary>Gets the taxonomies of the site, keyed by taxonomy name.</summary>
    public Dictionary<string, TaxonomyDefinition> Taxonomies { get; init; } = [];

    /// <summary>Gets the menus of the site, keyed by menu name.</summary>
    public Dictionary<string, Menu> Menus { get; init; } = [];

    /// <summary>Gets the site-level plugin settings, keyed by plugin name.</summary>
    public Dictionary<string, object> Plugins { get; init; } = [];

    /// <summary>Gets the theme settings, read from <c>themeConfig</c> and made available to the theme templates.</summary>
    public Dictionary<string, object> ThemeConfig { get; init; } = [];

    /// <summary>Gets the custom values from the <c>extra</c> section.</summary>
    public Dictionary<string, object> Extra { get; init; } = [];

    /// <summary>Gets the home page settings, or <see langword="null"/> when no home page is configured.</summary>
    public HomeConfiguration? Home { get; init; }

    /// <summary>Gets the settings of the production asset pipeline.</summary>
    public BuildOptions Build { get; init; } = new BuildOptions();

    /// <summary>Gets the asset settings.</summary>
    public AssetsOptions Assets { get; init; } = new AssetsOptions();

    /// <summary>Gets the search index settings.</summary>
    public SearchOptions Search { get; init; } = new SearchOptions();

    /// <summary>Gets the image optimization settings.</summary>
    public ImageOptions Images { get; init; } = new ImageOptions();

    /// <summary>
    /// Creates a copy of this configuration with a different base URL. All other values, including the
    /// collections, are shared with the original instance.
    /// </summary>
    /// <param name="baseUrl">The base URL of the copy.</param>
    /// <returns>The new configuration.</returns>
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
            Images = Images,
        };
    }

    /// <summary>
    /// Gets the base path of a base URL: its path without a trailing slash, or an empty string for the root.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <returns>The base path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseUrl"/> is <see langword="null"/>.</exception>
    public static string CalculateBasePath(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        var path = baseUrl.AbsolutePath;
        return string.IsNullOrWhiteSpace(path) || string.Equals(path, "/", StringComparison.Ordinal)
            ? string.Empty
            : path.TrimEnd('/');
    }

    /// <summary>
    /// Prefixes a site-relative URL with the base path. Fragment, query, <c>http(s)</c>, <c>mailto</c>, <c>tel</c>,
    /// <c>data</c> and <c>javascript</c> references are returned unchanged, as are URLs that already start with the base path.
    /// </summary>
    /// <param name="basePath">The base path, as returned by <see cref="CalculateBasePath"/>.</param>
    /// <param name="url">The URL to prefix.</param>
    /// <returns>The prefixed URL, or an empty string when <paramref name="url"/> is <see langword="null"/> or blank.</returns>
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

    /// <summary>
    /// Removes the base path from the start of a URL, together with any leading slashes.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="basePath">The base path, as returned by <see cref="CalculateBasePath"/>.</param>
    /// <returns>The URL without the base path, or an empty string when <paramref name="url"/> is <see langword="null"/> or blank.</returns>
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
