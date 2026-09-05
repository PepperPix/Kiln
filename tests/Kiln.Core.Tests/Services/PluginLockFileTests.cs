namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PluginLockFileTests
{
    [Test]
    public async Task SetAsync_CreatesLockFileAndPersistsEntries()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();

            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry(
                "Kiln.Plugin.EmailProtect",
                "1.0.0",
                "nuget"));

            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries).ContainsKey("email-protect");
            await Assert.That(entries["email-protect"].PackageId).IsEqualTo("Kiln.Plugin.EmailProtect");
            await Assert.That(entries["email-protect"].Version).IsEqualTo("1.0.0");

            var json = await File.ReadAllTextAsync(Path.Combine(projectDir, ".kiln", "plugins.lock.json"));
            await Assert.That(json).Contains("\"packageId\": \"Kiln.Plugin.EmailProtect\"");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task SetAsync_UpdatesExistingEntry_WithoutLosingOthers()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "alpha", new PluginLockEntry("Alpha.Plugin", "1.0.0", "nuget"));
            await lockFile.SetAsync(projectDir, "beta", new PluginLockEntry("Beta.Plugin", "2.0.0", "nuget"));
            await lockFile.SetAsync(projectDir, "alpha", new PluginLockEntry("Alpha.Plugin", "1.1.0", "nuget"));

            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries.Count).IsEqualTo(2);
            await Assert.That(entries["alpha"].Version).IsEqualTo("1.1.0");
            await Assert.That(entries["beta"].Version).IsEqualTo("2.0.0");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task RemoveAsync_DeletesEntryAndIsNoOp_WhenMissing()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "gamma", new PluginLockEntry("Gamma.Plugin", "3.0.0", "nuget"));
            await lockFile.RemoveAsync(projectDir, "gamma");
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries).IsEmpty();

            await lockFile.RemoveAsync(projectDir, "missing");
            var after = await lockFile.ReadAsync(projectDir);
            await Assert.That(after).IsEmpty();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }
}
