namespace Kiln.Services;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

public static class GeneratedContentSerializer
{
    public static string Serialize(IReadOnlyList<(string Key, object Value)> frontMatter, string body)
    {
        ArgumentNullException.ThrowIfNull(frontMatter);
        ArgumentNullException.ThrowIfNull(body);

        var root = new YamlMappingNode();
        foreach (var (key, value) in frontMatter)
            root.Add(new YamlScalarNode(key), BuildValueNode(value));

        using var sw = new StringWriter();
        new YamlStream(new YamlDocument(root)).Save(sw, assignAnchors: false);
        var yaml = NormalizeEmitted(sw.ToString());

        return "---\n" + yaml + "---\n\n" + body;
    }

    public static string ComputeBodyHash(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private static YamlNode BuildValueNode(object value)
    {
        switch (value)
        {
            case IDictionary<string, string> dict:
                var node = new YamlMappingNode();
                foreach (var kvp in dict)
                    node.Add(new YamlScalarNode(kvp.Key), new YamlScalarNode(kvp.Value));
                return node;

            case bool b:
                return new YamlScalarNode(b ? "true" : "false") { Style = ScalarStyle.Plain };

            case int i:
                return new YamlScalarNode(i.ToString(CultureInfo.InvariantCulture)) { Style = ScalarStyle.Plain };

            default:
                return new YamlScalarNode(value.ToString() ?? string.Empty);
        }
    }

    private static string NormalizeEmitted(string yaml)
    {
        const string docEndSuffix = "...\n";
        const string docEndMarker = "...";

        if (yaml.EndsWith(docEndSuffix, StringComparison.Ordinal))
            yaml = yaml[..^docEndSuffix.Length];
        else if (yaml.EndsWith(docEndMarker, StringComparison.Ordinal))
            yaml = yaml[..^docEndMarker.Length];

        return yaml.TrimEnd('\n') + "\n";
    }
}
