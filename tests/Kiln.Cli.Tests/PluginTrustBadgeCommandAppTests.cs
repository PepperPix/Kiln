namespace Kiln.Cli.Tests;

using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;

public class PluginTrustBadgeCommandAppTests
{
    [Test]
    public async Task Search_ShowsTrustBadgePerResult()
    {
        var client = new FakeNuGetPluginClient
        {
            SearchResults =
            [
                new PluginSearchResult("Kiln.Plugin.Good", "1.0.0", "Official") { Trust = PluginTrustLevel.FirstParty },
                new PluginSearchResult("Contoso.Widget", "2.0.0", "Third party"),
            ],
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "search", "x"]);

        var lines = console.Output.Split('\n');
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(lines.Single(l => l.Contains("Kiln.Plugin.Good", StringComparison.Ordinal))).Contains("first-party");
        await Assert.That(lines.Single(l => l.Contains("Contoso.Widget", StringComparison.Ordinal))).Contains("community");
        await Assert.That(lines.Single(l => l.Contains("Contoso.Widget", StringComparison.Ordinal))).DoesNotContain("first-party");
    }

    [Test]
    public async Task List_ShowsTrustFromLock_UnknownForLegacyEntriesAndManualForUntrackedPlugins()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-plugin-list-trust-{Guid.NewGuid():N}");
        try
        {
            foreach (var name in new[] { "official", "thirdparty", "legacy", "handmade" })
            {
                var dir = Path.Combine(projectDir, "plugins", name);
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(Path.Combine(dir, "plugin.yaml"), $"name: {name}\nversion: 1.0.0\n");
            }

            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "official", new PluginLockEntry("Kiln.Plugin.Official", "1.0.0", "nuget") { Trust = PluginTrustLevel.FirstParty });
            await lockFile.SetAsync(projectDir, "thirdparty", new PluginLockEntry("Contoso.Third", "1.0.0", "nuget") { Trust = PluginTrustLevel.Community });
            await lockFile.SetAsync(projectDir, "legacy", new PluginLockEntry("Contoso.Legacy", "1.0.0", "nuget"));

            var (app, console) = PluginTestApp.Create(new FakeNuGetPluginClient(), lockFile);
            var result = await app.RunAsync(["plugin", "list", projectDir]);

            var lines = console.Output.Split('\n');
            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(lines.Single(l => l.Contains("official", StringComparison.Ordinal))).Contains("first-party");
            await Assert.That(lines.Single(l => l.Contains("thirdparty", StringComparison.Ordinal))).Contains("community");
            await Assert.That(lines.Single(l => l.Contains("thirdparty", StringComparison.Ordinal))).DoesNotContain("unknown");
            await Assert.That(lines.Single(l => l.Contains("legacy", StringComparison.Ordinal))).Contains("community (unknown)");
            await Assert.That(lines.Single(l => l.Contains("handmade", StringComparison.Ordinal))).Contains("manual");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }
}
