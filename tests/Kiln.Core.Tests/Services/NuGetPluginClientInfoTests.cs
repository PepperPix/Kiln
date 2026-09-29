namespace Kiln.Core.Tests.Services;

using System.IO.Compression;
using System.Text;
using Kiln.Models;
using Kiln.Services;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

public class NuGetPluginClientInfoTests
{
    private const string Manifest = "name: good\nversion: 1.0.0\nslots:\n  - body_end\nshortcodes:\n  - email\n";

    [Test]
    public async Task GetInfoAsync_ReportsInventoryHostsMetadataAndInstallHash()
    {
        using var env = new InfoEnvironment();
        env.AddPackage("Contoso.Widget", Manifest,
        [
            ("content/static/a.js", "load('https://cdn.example.com/x.js');"),
            ("content/slots/body_end.html", "<script src=\"//cdn.other.example.org/y.js\"></script>"),
            ("content/static/icon.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
        ]);

        var info = await env.Client.GetInfoAsync("Contoso.Widget", null);
        var installed = await env.Client.AddAsync("Contoso.Widget", null, env.ProjectDir);

        await Assert.That(info.PackageId).IsEqualTo("Contoso.Widget");
        await Assert.That(info.Version).IsEqualTo("1.0.0");
        await Assert.That(info.PluginName).IsEqualTo("good");
        await Assert.That(info.Trust).IsEqualTo(PluginTrustLevel.Community);
        await Assert.That(info.Authors).IsEquivalentTo(new[] { "Contoso" });
        await Assert.That(info.Description).IsEqualTo("A test plugin");
        await Assert.That(info.License).IsEqualTo("MIT");
        await Assert.That(info.ProjectUrl).IsEqualTo(new Uri("https://example.com/widget"));
        await Assert.That(info.Slots).IsEquivalentTo(new[] { "body_end" });
        await Assert.That(info.Shortcodes).IsEquivalentTo(new[] { "email" });
        await Assert.That(info.ExternalHosts.SequenceEqual(["cdn.example.com", "cdn.other.example.org"])).IsTrue();
        await Assert.That(info.Inventory.Scripts).IsEqualTo(1);
        await Assert.That(info.Inventory.Html).IsEqualTo(1);
        await Assert.That(info.Inventory.Other).IsEqualTo(2);
        await Assert.That(info.Files.Select(f => f.Path).ToList()).Contains("static/a.js");
        await Assert.That(info.ContentHash).IsEqualTo(installed.ContentHash);
    }

    [Test]
    public async Task GetInfoAsync_InstallsNothingAndWritesNothingUnderThePlugins()
    {
        using var env = new InfoEnvironment();
        var marker = $"marker-{Guid.NewGuid():N}.txt";
        env.AddPackage("Contoso.Widget", Manifest, [($"content/{marker}", "x")]);
        var before = env.Snapshot();

        await env.Client.GetInfoAsync("Contoso.Widget", null);

        await Assert.That(env.Snapshot()).IsEquivalentTo(before);
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins"))).IsFalse();
        var leftovers = Directory.EnumerateDirectories(Path.GetTempPath(), "kiln-plugin-*")
            .Where(dir => File.Exists(Path.Combine(dir, "content", marker)))
            .ToList();
        await Assert.That(leftovers).IsEmpty();
    }

    [Test]
    public async Task GetInfoAsync_WithoutPluginTag_ThrowsAndCleansUp()
    {
        using var env = new InfoEnvironment();
        var marker = $"marker-{Guid.NewGuid():N}.txt";
        env.AddPackage("Contoso.Widget", Manifest, [($"content/{marker}", "x")], tags: "other");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => env.Client.GetInfoAsync("Contoso.Widget", null));

        await Assert.That(error!.Message).Contains("kiln-plugin");
        var leftovers = Directory.EnumerateDirectories(Path.GetTempPath(), "kiln-plugin-*")
            .Where(dir => File.Exists(Path.Combine(dir, "content", marker)))
            .ToList();
        await Assert.That(leftovers).IsEmpty();
    }

    [Test]
    public async Task GetInfoAsync_ForUnknownPackage_Throws()
    {
        using var env = new InfoEnvironment();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => env.Client.GetInfoAsync("Contoso.Missing", null));

        await Assert.That(error!.Message).Contains("was not found");
    }

    [Test]
    public async Task GetInfoAsync_ForUnknownVersion_Throws()
    {
        using var env = new InfoEnvironment();
        env.AddPackage("Contoso.Widget", Manifest, []);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => env.Client.GetInfoAsync("Contoso.Widget", "9.9.9"));

        await Assert.That(error!.Message).Contains("9.9.9");
    }

    [Test]
    public async Task GetInfoAsync_ForKilnPluginIdOnLocalFeed_IsCommunityBecauseNoReservationIsReported()
    {
        using var env = new InfoEnvironment();
        env.AddPackage("Kiln.Plugin.Good", Manifest, []);

        var info = await env.Client.GetInfoAsync("Kiln.Plugin.Good", null);

        await Assert.That(info.Trust).IsEqualTo(PluginTrustLevel.Community);
        await Assert.That(info.Owners).IsEmpty();
    }

    [Test]
    public async Task AddAsync_ForKilnPluginIdOnLocalFeed_ReportsCommunityTrust()
    {
        using var env = new InfoEnvironment();
        env.AddPackage("Kiln.Plugin.Good", Manifest, []);

        var result = await env.Client.AddAsync("Kiln.Plugin.Good", null, env.ProjectDir);

        await Assert.That(result.Trust).IsEqualTo(PluginTrustLevel.Community);
    }

    private sealed class InfoEnvironment : IDisposable
    {
        private readonly string _feedDir;

        public InfoEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), $"kiln-plugin-info-{Guid.NewGuid():N}");
            ProjectDir = Path.Combine(Root, "project");
            _feedDir = Path.Combine(Root, "feed");
            Directory.CreateDirectory(ProjectDir);
            Directory.CreateDirectory(_feedDir);
            Client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(_feedDir)));
        }

        public string Root { get; }

        public string ProjectDir { get; }

        public NuGetPluginClient Client { get; }

        public void AddPackage(string id, string manifest, IReadOnlyList<(string Name, string Text)> entries, string tags = "kiln-plugin")
        {
            using var stream = File.Create(Path.Combine(_feedDir, $"{id}.1.0.0.nupkg"));
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            WriteEntry(archive, $"{id}.nuspec",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><package><metadata>" +
                $"<id>{id}</id><version>1.0.0</version><description>A test plugin</description>" +
                $"<tags>{tags}</tags><authors>Contoso</authors>" +
                "<license type=\"expression\">MIT</license><projectUrl>https://example.com/widget</projectUrl>" +
                "</metadata></package>");
            WriteEntry(archive, "content/plugin.yaml", manifest);
            foreach (var (name, text) in entries)
                WriteEntry(archive, name, text);
        }

        public List<string> Snapshot()
        {
            var paths = Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, path))
                .Where(path => !path.StartsWith("feed", StringComparison.Ordinal))
                .ToList();
            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static void WriteEntry(ZipArchive archive, string name, string text)
        {
            using var entryStream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            entryStream.Write(Encoding.UTF8.GetBytes(text));
        }
    }
}
