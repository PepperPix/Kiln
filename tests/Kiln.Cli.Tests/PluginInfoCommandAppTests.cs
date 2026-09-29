namespace Kiln.Cli.Tests;

using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;

public class PluginInfoCommandAppTests
{
    [Test]
    public async Task PluginInfoCommand_ShowsTrustHostsHashAndContents_WithoutInstalling()
    {
        var client = new FakeNuGetPluginClient
        {
            InfoResult = PluginInfoFactory.Create("Kiln.Plugin.Good", "1.2.3", PluginTrustLevel.FirstParty, hosts: ["cdn.example.com"]),
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "info", "Kiln.Plugin.Good", "--version", "1.2.3"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Kiln.Plugin.Good 1.2.3");
        await Assert.That(console.Output).Contains("first-party");
        await Assert.That(console.Output).Contains("PepperPix");
        await Assert.That(console.Output).Contains("cdn.example.com");
        await Assert.That(console.Output).Contains("heuristic");
        await Assert.That(console.Output).Contains("hash-1.2.3");
        await Assert.That(console.Output).Contains("MIT");
        await Assert.That(console.Output).Contains("body_end");
        await Assert.That(client.LastInfoVersion).IsEqualTo("1.2.3");
        await Assert.That(client.AddCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task PluginInfoCommand_ForCommunityPackageWithoutHosts_ShowsCommunityAndNoHosts()
    {
        var client = new FakeNuGetPluginClient
        {
            InfoResult = PluginInfoFactory.Create("Contoso.Widget", "2.0.0", PluginTrustLevel.Community),
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "info", "Contoso.Widget"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("community");
        await Assert.That(console.Output).DoesNotContain("first-party");
        await Assert.That(console.Output).Contains("none found");
    }

    [Test]
    public async Task PluginInfoCommand_EscapesDynamicTextFromThePackage()
    {
        var client = new FakeNuGetPluginClient
        {
            InfoResult = PluginInfoFactory.Create("Contoso.[red]Widget", "1.0.0", PluginTrustLevel.Community, hosts: ["a[b].example.com"], description: "has [red]markup[/] inside"),
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "info", "Contoso.[red]Widget"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Contoso.[red]Widget");
        await Assert.That(console.Output).Contains("has [red]markup[/] inside");
        await Assert.That(console.Output).Contains("a[b].example.com");
    }

    [Test]
    public async Task PluginInfoCommand_TruncatesLongFileListsAfterTwentyEntries()
    {
        var files = Enumerable.Range(0, 25).Select(i => new PluginFileInfo($"static/f{i:D2}.js", 1)).ToList();
        var client = new FakeNuGetPluginClient
        {
            InfoResult = PluginInfoFactory.Create("Contoso.Widget", "1.0.0", PluginTrustLevel.Community) with { Files = files },
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "info", "Contoso.Widget"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("static/f19.js");
        await Assert.That(console.Output).DoesNotContain("static/f20.js");
        await Assert.That(console.Output).Contains("... and 5 more");
    }

    [Test]
    public async Task PluginInfoCommand_WithoutFeedMetadata_WarnsAndStillSucceeds()
    {
        var client = new FakeNuGetPluginClient
        {
            InfoResult = PluginInfoFactory.Create("Contoso.Widget", "1.0.0", PluginTrustLevel.Community) with { MetadataAvailable = false, Owners = [] },
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "info", "Contoso.Widget"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("metadata");
        await Assert.That(console.Output).Contains("not reported");
    }

    [Test]
    public async Task PluginInfoCommand_WhenPackageIsRejected_PrintsEscapedErrorAndExitsNonZero()
    {
        var client = new FakeNuGetPluginClient
        {
            InfoException = new InvalidOperationException("Package 'x[red]y' does not carry the 'kiln-plugin' tag."),
        };
        var (app, console) = PluginTestApp.Create(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "info", "x[red]y"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("ERROR:");
        await Assert.That(console.Output).Contains("x[red]y");
    }
}
