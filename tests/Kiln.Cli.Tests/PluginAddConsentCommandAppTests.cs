namespace Kiln.Cli.Tests;

using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Spectre.Console.Testing;

public class PluginAddConsentCommandAppTests
{
    [Test]
    public async Task Add_CommunityPackage_WhenConfirmed_InstallsShownVersionAndRecordsTrust()
    {
        using var project = new TempProject();
        var client = CommunityClient(hosts: ["cdn.example.com"]);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("y");

        var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("cdn.example.com");
        await Assert.That(console.Output).Contains("Install community plugin 'Contoso.Widget' 2.0.0");
        await Assert.That(client.AddCallCount).IsEqualTo(1);
        await Assert.That(client.LastAddVersion).IsEqualTo("2.0.0");
        var entry = (await new PluginLockFile().ReadAsync(project.Path))["widget"];
        await Assert.That(entry.Trust).IsEqualTo(PluginTrustLevel.Community);
    }

    [Test]
    public async Task Add_CommunityPackage_WhenDeclined_ExitsNonZeroAndInstallsNothing()
    {
        using var project = new TempProject();
        var client = CommunityClient();
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("n");

        var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(client.AddCallCount).IsEqualTo(0);
        await Assert.That(await new PluginLockFile().ReadAsync(project.Path)).IsEmpty();
    }

    [Test]
    public async Task Add_CommunityPackage_WhenJustEnterIsPressed_DefaultsToNo()
    {
        using var project = new TempProject();
        var client = CommunityClient();
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter(string.Empty);

        var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(client.AddCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task Add_CommunityPackage_NonInteractiveWithoutYes_FailsWithoutBlockingOrInstalling()
    {
        using var project = new TempProject();
        var client = CommunityClient(hosts: ["cdn.example.com"]);
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("Confirmation required for community plugins. Re-run with --yes.");
        await Assert.That(console.Output).Contains("cdn.example.com");
        await Assert.That(client.AddCallCount).IsEqualTo(0);
        await Assert.That(await new PluginLockFile().ReadAsync(project.Path)).IsEmpty();
    }

    [Test]
    [Arguments("--yes")]
    [Arguments("-y")]
    public async Task Add_CommunityPackage_WithYes_InstallsWithoutPrompting(string flag)
    {
        using var project = new TempProject();
        var client = CommunityClient();
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", flag, project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).DoesNotContain("Install community plugin");
        await Assert.That(client.AddCallCount).IsEqualTo(1);
        await Assert.That((await new PluginLockFile().ReadAsync(project.Path))["widget"].Trust).IsEqualTo(PluginTrustLevel.Community);
    }

    [Test]
    public async Task Add_FirstPartyPackage_InstallsWithoutPromptAndRecordsTrust()
    {
        using var project = new TempProject();
        var client = new FakeNuGetPluginClient
        {
            InfoResult = PluginInfoFactory.Create("Kiln.Plugin.Good", "1.0.0", PluginTrustLevel.FirstParty),
            InstallResult = new PluginPackageInstallResult("Kiln.Plugin.Good", "1.0.0", "good", "x") { Trust = PluginTrustLevel.FirstParty },
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
        console.Profile.Capabilities.Interactive = false;

        var result = await app.RunAsync(["plugin", "add", "Kiln.Plugin.Good", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("trustworthy source");
        await Assert.That(console.Output).DoesNotContain("Install community plugin");
        await Assert.That(client.InfoCallCount).IsEqualTo(1);
        await Assert.That((await new PluginLockFile().ReadAsync(project.Path))["good"].Trust).IsEqualTo(PluginTrustLevel.FirstParty);
    }

    [Test]
    public async Task Add_WhenInspectionFails_ExitsNonZeroWithoutInstalling()
    {
        using var project = new TempProject();
        var client = new FakeNuGetPluginClient { InfoException = new InvalidOperationException("Package 'a[b]' was not found.") };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "add", "a[b]", project.Path]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("a[b]");
        await Assert.That(client.AddCallCount).IsEqualTo(0);
    }

    private static FakeNuGetPluginClient CommunityClient(IReadOnlyList<string>? hosts = null)
        => new()
        {
            DefaultTrust = PluginTrustLevel.Community,
            InfoResult = PluginInfoFactory.Create("Contoso.Widget", "2.0.0", PluginTrustLevel.Community, hosts),
            InstallResult = new PluginPackageInstallResult("Contoso.Widget", "2.0.0", "widget", "x") { Trust = PluginTrustLevel.Community },
        };

    private sealed class TempProject : IDisposable
    {
        public TempProject()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kiln-plugin-consent-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
