namespace Kiln.Services;

using Kiln.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

        foreach (var pluginDir in Directory.EnumerateDirectories(pluginsDir))
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

            var dto = YamlDeserializer.Deserialize<PluginDefinitionDto>(File.ReadAllText(configPath));

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

            result.Add(definition);
        }

        return result;
    }

#pragma warning disable S3459, S1144 // Properties are assigned/read by YamlDotNet via reflection
    private sealed class PluginDefinitionDto
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
        public List<string>? Slots { get; set; }
    }
#pragma warning restore S3459, S1144
}
