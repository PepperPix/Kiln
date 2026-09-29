namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class PluginLockFileTrustTests
{
    [Test]
    [Arguments(PluginTrustLevel.FirstParty, "\"trust\": \"firstParty\"")]
    [Arguments(PluginTrustLevel.Community, "\"trust\": \"community\"")]
    public async Task SetAsync_WritesTrustAsCamelCaseString_AndReadsItBack(PluginTrustLevel trust, string expectedJson)
    {
        using var project = new TempProject();
        var lockFile = new PluginLockFile();

        await lockFile.SetAsync(project.Path, "good", new PluginLockEntry("Kiln.Plugin.Good", "1.0.0", "nuget") { Trust = trust });

        var json = await File.ReadAllTextAsync(project.LockPath);
        var entries = await lockFile.ReadAsync(project.Path);
        await Assert.That(json).Contains(expectedJson);
        await Assert.That(entries["good"].Trust).IsEqualTo(trust);
    }

    [Test]
    public async Task ReadAsync_LegacyLockWithoutTrust_IsReadableWithNullTrust()
    {
        using var project = new TempProject();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(project.LockPath)!);
        await File.WriteAllTextAsync(project.LockPath, """
            { "plugins": { "good": { "packageId": "Kiln.Plugin.Good", "version": "1.0.0", "source": "nuget", "contentHash": "abc" } } }
            """);

        var entries = await new PluginLockFile().ReadAsync(project.Path);

        await Assert.That(entries["good"].Trust).IsNull();
        await Assert.That(entries["good"].ContentHash).IsEqualTo("abc");
    }

    [Test]
    public async Task SetAsync_WithoutTrust_OmitsTheTrustField()
    {
        using var project = new TempProject();

        await new PluginLockFile().SetAsync(project.Path, "good", new PluginLockEntry("Kiln.Plugin.Good", "1.0.0", "nuget"));

        await Assert.That(await File.ReadAllTextAsync(project.LockPath)).DoesNotContain("trust");
    }

    private sealed class TempProject : IDisposable
    {
        public TempProject()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kiln-lock-trust-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string LockPath => System.IO.Path.Combine(Path, ".kiln", "plugins.lock.json");

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
