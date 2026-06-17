namespace Kiln.Services;

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
        return YamlDeserializer.Deserialize<SiteConfiguration>(content)
            ?? throw new InvalidOperationException($"Failed to parse site configuration: {configPath}");
    }
}
