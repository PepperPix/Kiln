namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PluginLoaderOrderAndManifestTests
{
    private readonly PluginLoader _loader = new();

    [Test]
    public async Task LoadPlugins_ReturnsPluginsInOrdinalNameOrder_RegardlessOfCreationOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-order-{Guid.NewGuid():N}");
        try
        {
            foreach (var name in new[] { "b", "a", "c" })
            {
                var pluginDir = Path.Combine(dir, "plugins", name);
                Directory.CreateDirectory(pluginDir);
                await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"), $"name: Plugin {name}\n");
            }

            var plugins = _loader.LoadPlugins(dir);

            await Assert.That(plugins.Select(p => Path.GetFileName(p.Directory)).ToArray())
                .IsEquivalentTo(new[] { "a", "b", "c" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task LoadPlugins_InvalidYaml_ThrowsWithManifestPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-bad-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(dir, "plugins", "broken");
        Directory.CreateDirectory(pluginDir);
        var manifest = Path.Combine(pluginDir, "plugin.yaml");
        await File.WriteAllTextAsync(manifest, "name: [unclosed\nslots: {");
        try
        {
            await Assert.That(() => _loader.LoadPlugins(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining($"Invalid plugin manifest '{manifest}'");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
