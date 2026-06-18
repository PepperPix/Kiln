namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class SiteConfigLoader : ISiteConfigLoader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public SiteConfiguration Load(string projectPath)
    {
        var yamlPath = Path.Combine(projectPath, "site.yaml");
        var ymlPath = Path.Combine(projectPath, "site.yml");

        string configPath;
        if (File.Exists(yamlPath))
            configPath = yamlPath;
        else if (File.Exists(ymlPath))
            configPath = ymlPath;
        else
            throw new FileNotFoundException($"No site.yaml or site.yml found in: {projectPath}");

        var content = File.ReadAllText(configPath);
        var dto = YamlDeserializer.Deserialize<SiteConfigDto>(content)
            ?? throw new InvalidOperationException($"Failed to parse site configuration: {configPath}");

        return MapToConfig(dto);
    }

    private static SiteConfiguration MapToConfig(SiteConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("site.yaml is missing required field: title");
        if (string.IsNullOrWhiteSpace(dto.RootAddress))
            throw new InvalidOperationException("site.yaml is missing required field: baseUrl");

        var collections = new Dictionary<string, ContentGroup>();
        if (dto.Collections is not null)
        {
            foreach (var (name, colDto) in dto.Collections)
            {
                collections[name] = MapCollection(name, colDto);
            }
        }

        var taxonomies = new Dictionary<string, TaxonomyDefinition>();
        if (dto.Taxonomies is not null)
        {
            foreach (var (name, taxDto) in dto.Taxonomies)
            {
                taxonomies[name] = new TaxonomyDefinition
                {
                    Name = name,
                    Permalink = taxDto.Permalink ?? "/:slug/",
                    Paginate = taxDto.Paginate
                };
            }
        }

        var menus = new Dictionary<string, Menu>();
        if (dto.Menus is not null)
        {
            foreach (var (name, itemDtos) in dto.Menus)
            {
                menus[name] = new Menu
                {
                    Name = name,
                    Items = new System.Collections.ObjectModel.Collection<MenuItem>(itemDtos.Select(MapMenuItem).ToList())
                };
            }
        }

        return new SiteConfiguration
        {
            Title = dto.Title,
            Description = dto.Description,
            BaseUrl = new Uri(dto.RootAddress!),
            Language = dto.Language ?? "en",
            Theme = dto.Theme ?? "default",
            AssetPrefix = dto.AssetPrefix ?? "/assets/",
            OutputDir = dto.OutputDir ?? "_site",
            ThemesDir = dto.ThemesDir ?? "themes",
            Collections = collections,
            Taxonomies = taxonomies,
            Menus = menus,
            Plugins = dto.Plugins ?? [],
            ThemeConfig = dto.ThemeConfig ?? [],
            Extra = dto.Extra ?? []
        };
    }

    private static MenuItem MapMenuItem(MenuItemDto dto)
    {
        return new MenuItem
        {
            Title = dto.Title ?? string.Empty,
            Url = dto.Url is not null ? new Uri(dto.Url, UriKind.RelativeOrAbsolute) : null,
            Ref = dto.Ref,
            External = dto.External,
            Children = new System.Collections.ObjectModel.Collection<MenuItem>(dto.Children?.Select(MapMenuItem).ToList() ?? [])
        };
    }

    private static ContentGroup MapCollection(string name, CollectionDto dto)
    {
        var taxonomies = new Collection<string>(dto.Taxonomies ?? []);
        return new ContentGroup
        {
            Name = name,
            Directory = dto.Directory ?? $"content/{name}",
            Permalink = dto.Permalink ?? "/:slug/",
            Sort = dto.Sort ?? "none",
            Feed = dto.Feed,
            Paginate = dto.Paginate,
            Layout = dto.Layout ?? "default",
            Taxonomies = taxonomies,
            References = dto.References ?? [],
            Plugins = dto.Plugins ?? [],
            Extra = dto.Extra ?? []
        };
    }

    // DTOs for YAML deserialization — properties are assigned by YamlDotNet via reflection

#pragma warning disable S3459, S1144, S3996 // Properties are assigned/read by YamlDotNet via reflection

    private sealed class SiteConfigDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        [YamlMember(Alias = "baseUrl")]
        public string? RootAddress { get; set; }
        public string? Language { get; set; }
        public string? Theme { get; set; }
        public string? AssetPrefix { get; set; }
        public string? OutputDir { get; set; }
        public string? ThemesDir { get; set; }
        public Dictionary<string, CollectionDto>? Collections { get; set; }
        public Dictionary<string, TaxonomyDto>? Taxonomies { get; set; }
        public Dictionary<string, List<MenuItemDto>>? Menus { get; set; }
        public Dictionary<string, object>? Plugins { get; set; }
        public Dictionary<string, object>? ThemeConfig { get; set; }
        public Dictionary<string, object>? Extra { get; set; }
    }

    private sealed class MenuItemDto
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Ref { get; set; }
        public bool External { get; set; }
        public List<MenuItemDto>? Children { get; set; }
    }

    private sealed class CollectionDto
    {
        public string? Directory { get; set; }
        public string? Permalink { get; set; }
        public string? Sort { get; set; }
        public bool Feed { get; set; }
        public int? Paginate { get; set; }
        public string? Layout { get; set; }
        public List<string>? Taxonomies { get; set; }
        public Dictionary<string, string>? References { get; set; }
        public Dictionary<string, object>? Plugins { get; set; }
        public Dictionary<string, object>? Extra { get; set; }
    }

    private sealed class TaxonomyDto
    {
        public string? Permalink { get; set; }
        public int? Paginate { get; set; }
    }

#pragma warning restore S3459, S1144, S3996
}

