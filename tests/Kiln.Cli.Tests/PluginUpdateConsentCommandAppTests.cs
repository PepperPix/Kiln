namespace Kiln.Cli.Tests;

using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Spectre.Console.Testing;

public class PluginUpdateConsentCommandAppTests
{
    [Test]
    public async Task Update_WithoutNewHostsOrTrustDrop_ProceedsWithoutPrompting()
    {
        using var project = await TempProject.CreateAsync("widget", "1.0.0", PluginTrustLevel.Community, installedHost: "cdn.example.com");
        var client = Client(PluginTrustLevel.Community, "2.0.0", hosts: ["cdn.example.com", "CDN.EXAMPLE.COM"]);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "update", "widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("1.0.0 -> 2.0.0");
        await Assert.That(client.AddCallCount).IsEqualTo(1);
        await Assert.That((await new PluginLockFile().ReadAsync(project.Path))["widget"].Version).IsEqualTo("2.0.0");
    }

    [Test]
    public async Task Update_WithNewHost_WhenConfirmed_UpdatesAndShowsTheNewHost()
    {
        using var project = await TempProject.CreateAsync("widget", "1.0.0", PluginTrustLevel.Community, installedHost: "cdn.example.com");
        var client = Client(PluginTrustLevel.Community, "2.0.0", hosts: ["cdn.example.com", "tracker.example.net"]);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("y");

        var result = await app.RunAsync(["plugin", "update", "widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("tracker.example.net");
        await Assert.That(client.AddCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Update_WithNewHost_WhenDeclined_KeepsInstalledVersion()
    {
        using var project = await TempProject.CreateAsync("widget", "1.0.0", PluginTrustLevel.Community, installedHost: "cdn.example.com");
        var client = Client(PluginTrustLevel.Community, "2.0.0", hosts: ["cdn.example.com", "tracker.example.net"]);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("n");

        var result = await app.RunAsync(["plugin", "update", "widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(client.AddCallCount).IsEqualTo(0);
        await Assert.That((await new PluginLockFile().ReadAsync(project.Path))["widget"].Version).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task Update_WithNewHost_NonInteractiveWithoutYes_FailsWithoutUpdating()
    {
        using var project = await TempProject.CreateAsync("widget", "1.0.0", PluginTrustLevel.Community);
        var client = Client(PluginTrustLevel.Community, "2.0.0", hosts: ["tracker.example.net"]);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "update", "widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("Re-run with --yes");
        await Assert.That(client.AddCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Update_WhenFirstPartyBecomesCommunity_AsksForConfirmation()
    {
        using var project = await TempProject.CreateAsync("widget", "1.0.0", PluginTrustLevel.FirstParty);
        var client = Client(PluginTrustLevel.Community, "2.0.0");
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "update", "widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("no longer first-party");
        await Assert.That(client.AddCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Update_WhenLockHasNoTrust_AndPackageIsCommunity_DoesNotCountAsDrop()
    {
        using var project = await TempProject.CreateAsync("widget", "1.0.0", trust: null);
        var client = Client(PluginTrustLevel.Community, "2.0.0");
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "update", "widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(client.AddCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Update_WithYes_SkipsInspectionAndPrompt_AndRecordsNewTrust()
    {
        using var project = await TempProject.CreateAsync("widget", "1.0.0", PluginTrustLevel.FirstParty);
        var client = Client(PluginTrustLevel.Community, "2.0.0", hosts: ["tracker.example.net"]);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "update", "widget", "-y", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(client.InfoCallCount).IsEqualTo(0);
        await Assert.That(client.AddCallCount).IsEqualTo(1);
        await Assert.That((await new PluginLockFile().ReadAsync(project.Path))["widget"].Trust).IsEqualTo(PluginTrustLevel.Community);
    }

    [Test]
    public async Task Update_All_ContinuesWithOtherPluginsWhenOneNeedsConsent()
    {
        using var project = await TempProject.CreateAsync("alpha", "1.0.0", PluginTrustLevel.Community);
        await new PluginLockFile().SetAsync(project.Path, "beta", new PluginLockEntry("Contoso.Beta", "1.0.0", "nuget") { Trust = PluginTrustLevel.Community });
        var client = Client(PluginTrustLevel.Community, "2.0.0");
        client.InfoFactory = (id, version) => id == "Contoso.Alpha"
            ? PluginInfoFactory.Create(id, version, PluginTrustLevel.Community, ["tracker.example.net"])
            : PluginInfoFactory.Create(id, version, PluginTrustLevel.Community);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "update", "--all", "ignored-name", project.Path]);

        var entries = await new PluginLockFile().ReadAsync(project.Path);
        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(entries["alpha"].Version).IsEqualTo("1.0.0");
        await Assert.That(entries["beta"].Version).IsEqualTo("2.0.0");
    }

    private static FakeNuGetPluginClient Client(PluginTrustLevel trust, string latest, IReadOnlyList<string>? hosts = null)
        => new()
        {
            LatestVersion = latest,
            DefaultTrust = trust,
            InfoResult = hosts is null ? null : PluginInfoFactory.Create("Contoso.Widget", latest, trust, hosts),
        };

    private sealed class TempProject : IDisposable
    {
        private TempProject()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kiln-plugin-update-consent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public static async Task<TempProject> CreateAsync(string name, string version, PluginTrustLevel? trust, string? installedHost = null)
        {
            var project = new TempProject();
            var packageId = $"Contoso.{char.ToUpperInvariant(name[0])}{name[1..]}";
            var pluginDir = System.IO.Path.Combine(project.Path, "plugins", name);
            Directory.CreateDirectory(pluginDir);
            await File.WriteAllTextAsync(
                System.IO.Path.Combine(pluginDir, "plugin.js"),
                installedHost is null ? "var a = 1;" : $"load('https://{installedHost}/x.js');");
            await new PluginLockFile().SetAsync(project.Path, name, new PluginLockEntry(packageId, version, "nuget") { Trust = trust });
            return project;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
