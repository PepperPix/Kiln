namespace Kiln.Core.Tests.Services;

using System.IO.Compression;
using System.Text;
using Kiln.Services;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

public class NuGetPluginClientSafetyTests
{
    private const int EntryLimitPlusOne = 2001;
    private const int MebiByte = 1024 * 1024;

    [Test]
    [Arguments("../../evil-NAME")]
    [Arguments("/abs/evil-NAME")]
    [Arguments("a/b")]
    [Arguments("a\\b")]
    [Arguments(".")]
    [Arguments("..")]
    [Arguments("Evil Name")]
    [Arguments("UPPER")]
    [Arguments("")]
    [Arguments("   ")]
    public async Task AddAsync_WithInvalidManifestName_ThrowsAndWritesNothing(string manifestName)
    {
        using var env = new TestEnvironment();
        var unique = Guid.NewGuid().ToString("N");
        var name = manifestName.Replace("evil-NAME", $"evil-{unique}", StringComparison.Ordinal);
        env.AddPackage("Kiln.Plugin.Evil", $"name: '{name}'\nversion: 1.0.0\n", []);
        var before = env.Snapshot();

        var error = await TryAddAsync(env, "Kiln.Plugin.Evil");

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(env.Snapshot()).IsEquivalentTo(before);
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins"))).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(env.Root, $"evil-{unique}"))).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(Path.GetTempPath(), $"evil-{unique}"))).IsFalse();
    }

    [Test]
    [Arguments("content/../escape.txt")]
    [Arguments("content/../content-evil/payload.txt")]
    [Arguments("content/sub/../../escape.txt")]
    [Arguments("content/..\\escape.txt")]
    public async Task AddAsync_WithTraversingArchiveEntry_ThrowsAndInstallsNothing(string entryName)
    {
        using var env = new TestEnvironment();
        env.AddPackage("Kiln.Plugin.Slip", "name: slip\n", [new ArchiveEntry(entryName, "payload")]);

        var error = await TryAddAsync(env, "Kiln.Plugin.Slip");

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins", "slip"))).IsFalse();
    }

    [Test]
    public async Task AddAsync_WithRootedArchiveEntry_ThrowsAndDoesNotWriteOutsideTheExtractionRoot()
    {
        using var env = new TestEnvironment();
        var escapeDir = Path.Combine(env.Root, $"rooted-escape-{Guid.NewGuid():N}");
        env.AddPackage("Kiln.Plugin.Rooted", "name: rooted\n", [new ArchiveEntry($"content/{escapeDir}/payload.txt", "payload")]);

        var error = await TryAddAsync(env, "Kiln.Plugin.Rooted");

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(escapeDir)).IsFalse();
    }

    [Test]
    public async Task AddAsync_WithTooManyEntries_Throws()
    {
        using var env = new TestEnvironment();
        var entries = Enumerable.Range(0, EntryLimitPlusOne)
            .Select(i => new ArchiveEntry($"content/files/f{i}.txt", "x"))
            .ToList();
        env.AddPackage("Kiln.Plugin.Many", "name: many\n", entries);

        var error = await TryAddAsync(env, "Kiln.Plugin.Many");

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins", "many"))).IsFalse();
    }

    [Test]
    public async Task AddAsync_WithOversizedEntry_Throws()
    {
        using var env = new TestEnvironment();
        env.AddPackage("Kiln.Plugin.Big", "name: big\n", [new ArchiveEntry("content/big.bin", new byte[(10 * MebiByte) + 1])]);

        var error = await TryAddAsync(env, "Kiln.Plugin.Big");

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins", "big"))).IsFalse();
    }

    [Test]
    public async Task AddAsync_WithOversizedTotal_Throws()
    {
        using var env = new TestEnvironment();
        var entries = Enumerable.Range(0, 6)
            .Select(i => new ArchiveEntry($"content/part{i}.bin", new byte[9 * MebiByte]))
            .ToList();
        env.AddPackage("Kiln.Plugin.Total", "name: total\n", entries);

        var error = await TryAddAsync(env, "Kiln.Plugin.Total");

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins", "total"))).IsFalse();
    }

    [Test]
    public async Task AddAsync_WithQuotedManifestName_InstallsUnderThatName()
    {
        using var env = new TestEnvironment();
        env.AddPackage("Kiln.Plugin.EmailProtect", "name: \"email-protect\"\nversion: 1.0.0\n", [new ArchiveEntry("content/static/plugin.js", "console.log(1);")]);

        var result = await env.Client.AddAsync("Kiln.Plugin.EmailProtect", null, env.ProjectDir);

        await Assert.That(result.PluginName).IsEqualTo("email-protect");
        await Assert.That(File.Exists(Path.Combine(env.ProjectDir, "plugins", "email-protect", "plugin.yaml"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(env.ProjectDir, "plugins", "email-protect", "static", "plugin.js"))).IsTrue();
    }

    [Test]
    public async Task AddAsync_WithoutManifestName_FallsBackToValidPackageShortName()
    {
        using var env = new TestEnvironment();
        env.AddPackage("Kiln.Plugin.short-name", "version: 1.0.0\n", []);

        var result = await env.Client.AddAsync("Kiln.Plugin.short-name", null, env.ProjectDir);

        await Assert.That(result.PluginName).IsEqualTo("short-name");
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins", "short-name"))).IsTrue();
    }

    [Test]
    public async Task AddAsync_WithoutManifestName_AndInvalidPackageShortName_Throws()
    {
        using var env = new TestEnvironment();
        env.AddPackage("Kiln.Plugin.EmailProtect", "version: 1.0.0\n", []);

        var error = await TryAddAsync(env, "Kiln.Plugin.EmailProtect");

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(Directory.Exists(Path.Combine(env.ProjectDir, "plugins"))).IsFalse();
    }

    [Test]
    public async Task AddAsync_OverExistingInstall_RemovesStaleFiles()
    {
        using var env = new TestEnvironment();
        var pluginDir = Path.Combine(env.ProjectDir, "plugins", "email-protect");
        Directory.CreateDirectory(Path.Combine(pluginDir, "static"));
        await File.WriteAllTextAsync(Path.Combine(pluginDir, "stale.txt"), "old");
        await File.WriteAllTextAsync(Path.Combine(pluginDir, "static", "old.js"), "old");
        var sibling = Path.Combine(env.ProjectDir, "plugins", "other");
        Directory.CreateDirectory(sibling);
        await File.WriteAllTextAsync(Path.Combine(sibling, "keep.txt"), "keep");
        env.AddPackage("Kiln.Plugin.EmailProtect", "name: email-protect\n", [new ArchiveEntry("content/static/new.js", "new")]);

        await env.Client.AddAsync("Kiln.Plugin.EmailProtect", null, env.ProjectDir);

        await Assert.That(File.Exists(Path.Combine(pluginDir, "stale.txt"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(pluginDir, "static", "old.js"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(pluginDir, "static", "new.js"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(sibling, "keep.txt"))).IsTrue();
    }

    private static async Task<Exception?> TryAddAsync(TestEnvironment env, string packageId)
    {
        try
        {
            await env.Client.AddAsync(packageId, null, env.ProjectDir);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    private sealed record ArchiveEntry(string Name, byte[] Data)
    {
        public ArchiveEntry(string name, string text)
            : this(name, Encoding.UTF8.GetBytes(text))
        {
        }
    }

    private sealed class TestEnvironment : IDisposable
    {
        private readonly string _feedDir;

        public TestEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), $"kiln-plugin-safety-{Guid.NewGuid():N}");
            ProjectDir = Path.Combine(Root, "project");
            _feedDir = Path.Combine(Root, "feed");
            Directory.CreateDirectory(ProjectDir);
            Directory.CreateDirectory(_feedDir);
            File.WriteAllText(Path.Combine(ProjectDir, "site.yaml"), "title: Keep\n");
            File.WriteAllText(Path.Combine(Root, "outer.txt"), "outer");
            Client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(_feedDir)));
        }

        public string Root { get; }

        public string ProjectDir { get; }

        public NuGetPluginClient Client { get; }

        public void AddPackage(string id, string manifest, IReadOnlyList<ArchiveEntry> contentEntries)
        {
            using var stream = File.Create(Path.Combine(_feedDir, $"{id}.1.0.0.nupkg"));
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            WriteEntry(archive, $"{id}.nuspec", Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><package><metadata>" +
                $"<id>{id}</id><version>1.0.0</version><description>test</description>" +
                "<tags>kiln-plugin</tags><authors>Test</authors></metadata></package>"));
            WriteEntry(archive, "content/plugin.yaml", Encoding.UTF8.GetBytes(manifest));
            foreach (var entry in contentEntries)
                WriteEntry(archive, entry.Name, entry.Data);
        }

        public List<string> Snapshot()
        {
            var entries = Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, path))
                .Where(path => !path.StartsWith("feed", StringComparison.Ordinal))
                .ToList();
            entries.Sort(StringComparer.Ordinal);
            return entries;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private static void WriteEntry(ZipArchive archive, string name, byte[] data)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(data);
        }
    }
}
