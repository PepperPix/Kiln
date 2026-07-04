namespace Kiln.Core.Tests.Services;

using Kiln.Services;
using YamlDotNet.Serialization;

public class GeneratedContentSerializerTests
{
    [Test]
    public async Task Serialize_RoundTrip_PreservesDangerousValues()
    {
        var frontMatter = new List<(string Key, object Value)>
        {
            ("title", "a: b, yes # x"),
            ("weight", 42),
            ("generated", true),
            ("source_hash", "abc123def456"),
            ("extra", new Dictionary<string, string>
            {
                ["method"] = "GET",
                ["path"] = "/pets/{id}",
            }),
        };

        const string body = "# Test\ncontent\n";
        var result = GeneratedContentSerializer.Serialize(frontMatter, body);

        var yamlEnd = result.IndexOf("\n---\n", StringComparison.Ordinal);
        await Assert.That(yamlEnd).IsGreaterThan(0);
        var yamlBlock = result[4..yamlEnd];

        var deserialized = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object>>(yamlBlock);

        await Assert.That(deserialized["title"].ToString()).IsEqualTo("a: b, yes # x");
        await Assert.That(deserialized["weight"].ToString()).IsEqualTo("42");
        await Assert.That(deserialized["generated"].ToString() is "True" or "true").IsTrue();
        await Assert.That(deserialized["source_hash"].ToString()).IsEqualTo("abc123def456");

        var extra = deserialized["extra"] as Dictionary<object, object>;
        await Assert.That(extra).IsNotNull();
        await Assert.That(extra!["method"].ToString()).IsEqualTo("GET");
        await Assert.That(extra["path"].ToString()).IsEqualTo("/pets/{id}");
    }
}
