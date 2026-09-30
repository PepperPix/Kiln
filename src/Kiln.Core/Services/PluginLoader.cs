namespace Kiln.Services;

using Kiln.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Default <see cref="IPluginLoader"/> implementation that reads the <c>plugin.yaml</c> manifests in the <c>plugins</c> directory.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on <see cref="IPluginLoader"/> instead.
/// </remarks>
public sealed class PluginLoader : IPluginLoader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public IReadOnlyList<PluginDefinition> LoadPlugins(string projectPath)
    {
        var pluginsDir = Path.Combine(projectPath, "plugins");
        if (!Directory.Exists(pluginsDir))
            return [];

        var result = new List<PluginDefinition>();

        var pluginDirs = Directory.EnumerateDirectories(pluginsDir)
            .OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal);

        foreach (var pluginDir in pluginDirs)
        {
            var yamlPath = Path.Combine(pluginDir, "plugin.yaml");
            var ymlPath = Path.Combine(pluginDir, "plugin.yml");

            string? configPath = null;
            if (File.Exists(yamlPath))
                configPath = yamlPath;
            else if (File.Exists(ymlPath))
                configPath = ymlPath;

            if (configPath is null)
                continue;

            PluginDefinitionDto dto;
            try
            {
                dto = YamlDeserializer.Deserialize<PluginDefinitionDto>(File.ReadAllText(configPath));
            }
            catch (YamlException ex)
            {
                throw new InvalidOperationException($"Invalid plugin manifest '{configPath}': {ex.Message}", ex);
            }

            var pluginName = Path.GetFileName(pluginDir);
            var definition = new PluginDefinition
            {
                Name = string.IsNullOrWhiteSpace(dto.Name) ? pluginName : dto.Name,
                Version = dto.Version,
                Description = dto.Description,
                Directory = pluginDir
            };

            if (dto.Slots is not null)
                foreach (var slot in dto.Slots)
                    definition.Slots.Add(slot);

            if (dto.Shortcodes is not null)
                foreach (var shortcode in dto.Shortcodes)
                    definition.Shortcodes.Add(shortcode);

            result.Add(definition);
        }

        return result;
    }

    private sealed class PluginDefinitionDto
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
        public List<string>? Slots { get; set; }
        public List<string>? Shortcodes { get; set; }
    }
}
