namespace Kiln.Core.Tests.Services;

using System.IO.Compression;
using Kiln.Services;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

public class NuGetPluginClientTests
{
    [Test]
    public async Task SearchAsync_FindsKilnPluginPackagesInLocalSource()
    {
        var sourceRoot = CreateLocalPackageSource();
        var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

        var results = await client.SearchAsync("kiln-plugin");

        await Assert.That(results.Count >= 1).IsTrue();
        await Assert.That(results[0].Id).IsEqualTo("Kiln.Plugin.EmailProtect");
        await Assert.That(results[0].Version).IsEqualTo("1.3.0");
        await Assert.That(results[0].Description).IsEqualTo("Protect emails from scrapers");
    }

    [Test]
    public async Task GetLatestVersionAsync_ResolvesHighestStableVersion()
    {
        var sourceRoot = CreateLocalPackageSource();
        var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

        var version = await client.GetLatestVersionAsync("Kiln.Plugin.EmailProtect");

        await Assert.That(version).IsEqualTo("1.3.0");
    }

    [Test]
    public async Task GetLatestVersionAsync_WhenPackageDoesNotExist_ReturnsNull()
    {
        var sourceRoot = CreateLocalPackageSource();
        var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

        var version = await client.GetLatestVersionAsync("Kiln.Plugin.DoesNotExist");

        await Assert.That(version).IsNull();
    }

    [Test]
    public async Task AddAsync_ExtractsPluginContent_IntoPluginsDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-nuget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceRoot = CreateLocalPackageSource();
            var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

            var result = await client.AddAsync("Kiln.Plugin.EmailProtect", null, tempRoot);

            await Assert.That(result.PluginName).IsEqualTo("email-protect");
            await Assert.That(result.PackageId).IsEqualTo("Kiln.Plugin.EmailProtect");
            await Assert.That(result.Version).IsEqualTo("1.3.0");
            await Assert.That(File.Exists(Path.Combine(tempRoot, "plugins", "email-protect", "plugin.yaml"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempRoot, "plugins", "email-protect", "static", "plugin.js"))).IsTrue();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateLocalPackageSource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kiln-nuget-feed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        CreatePackage(
            root,
            "Kiln.Plugin.EmailProtect",
            "1.2.3",
            "email-protect",
            "Protect emails from scrapers");
        CreatePackage(
            root,
            "Kiln.Plugin.EmailProtect",
            "1.3.0",
            "email-protect",
            "Protect emails from scrapers");

        return root;
    }

    private static void CreatePackage(string root, string id, string version, string pluginName, string description)
    {
        var packagePath = Path.Combine(root, $"{id}.{version}.nupkg");

        using var stream = File.Create(packagePath);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var nuspec = archive.CreateEntry($"{id}.nuspec");
            using (var writer = new StreamWriter(nuspec.Open()))
            {
                writer.Write(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<package><metadata>" +
                    $"<id>{id}</id><version>{version}</version>" +
                    $"<description>{description}</description>" +
                    "<tags>kiln-plugin email privacy</tags>" +
                    "<authors>Test</authors>" +
                    "</metadata></package>");
            }

            var manifest = archive.CreateEntry("content/plugin.yaml");
            using (var writer = new StreamWriter(manifest.Open()))
            {
                writer.Write($"name: {pluginName}\nversion: {version}\ndescription: {description}\n");
            }

            var script = archive.CreateEntry("content/static/plugin.js");
            using (var writer = new StreamWriter(script.Open()))
            {
                writer.Write("console.log('hello');\n");
            }
        }
    }
}
