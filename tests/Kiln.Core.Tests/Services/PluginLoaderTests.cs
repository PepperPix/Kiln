namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class PluginLoaderTests
{
    private readonly PluginLoader _loader = new();

    [Test]
    public async Task LoadPlugins_ReturnsEmpty_WhenNoPluginsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-noplugins-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var plugins = _loader.LoadPlugins(dir);
            await Assert.That(plugins).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task LoadPlugins_SkipsDirectory_WithoutPluginYaml()
    {
        var dir = CreateProjectWithPlugin(
            pluginName: "no-yaml-plugin",
            yamlContent: null,
            slotFiles: []);
        try
        {
            var plugins = _loader.LoadPlugins(dir);
            await Assert.That(plugins).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task LoadPlugins_ParsesNameAndSlots()
    {
        var dir = CreateProjectWithPlugin(
            pluginName: "my-plugin",
            yamlContent: """
                name: My Plugin
                version: 1.2.3
                description: A test plugin
                slots:
                  - after_content
                  - body_end
                """,
            slotFiles: ["after_content.html", "body_end.html"]);
        try
        {
            var plugins = _loader.LoadPlugins(dir);
            await Assert.That(plugins.Count).IsEqualTo(1);

            var p = plugins[0];
            await Assert.That(p.Name).IsEqualTo("My Plugin");
            await Assert.That(p.Version).IsEqualTo("1.2.3");
            await Assert.That(p.Description).IsEqualTo("A test plugin");
            await Assert.That(p.Slots).Contains("after_content");
            await Assert.That(p.Slots).Contains("body_end");
            await Assert.That(p.Directory).IsEqualTo(
                Path.Combine(dir, "plugins", "my-plugin"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task LoadPlugins_FallsBackToDirectoryNameWhenNameMissing()
    {
        var dir = CreateProjectWithPlugin(
            pluginName: "disqus",
            yamlContent: """
                slots:
                  - after_content
                """,
            slotFiles: []);
        try
        {
            var plugins = _loader.LoadPlugins(dir);
            await Assert.That(plugins.Count).IsEqualTo(1);
            await Assert.That(plugins[0].Name).IsEqualTo("disqus");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task LoadPlugins_LoadsMultiplePlugins()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-multi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "plugins", "plugin-a"));
        Directory.CreateDirectory(Path.Combine(dir, "plugins", "plugin-b"));
        File.WriteAllText(Path.Combine(dir, "plugins", "plugin-a", "plugin.yaml"),
            "name: Plugin A\nslots:\n  - head\n");
        File.WriteAllText(Path.Combine(dir, "plugins", "plugin-b", "plugin.yaml"),
            "name: Plugin B\nslots:\n  - body_end\n");
        try
        {
            var plugins = _loader.LoadPlugins(dir);
            await Assert.That(plugins.Count).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateProjectWithPlugin(
        string pluginName,
        string? yamlContent,
        IEnumerable<string> slotFiles)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(dir, "plugins", pluginName);
        Directory.CreateDirectory(pluginDir);
        Directory.CreateDirectory(Path.Combine(pluginDir, "slots"));

        if (yamlContent is not null)
            File.WriteAllText(Path.Combine(pluginDir, "plugin.yaml"), yamlContent);

        foreach (var slotFile in slotFiles)
            File.WriteAllText(Path.Combine(pluginDir, "slots", slotFile), $"<div>{slotFile}</div>");

        return dir;
    }
}
