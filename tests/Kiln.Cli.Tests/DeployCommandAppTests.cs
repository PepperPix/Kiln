namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class DeployCommandAppTests
{
    private static (CommandAppTester App, TestConsole Console, FakeDeploymentInitializer Initializer) CreateApp()
    {
        var initializer = new FakeDeploymentInitializer();

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<IDeploymentInitializer>(initializer);
        });

        app.Configure(config => config.AddCommand<DeployCommand>("deploy"));

        return (app, console, initializer);
    }

    [Test]
    public async Task DeployCommand_UnknownTarget_ExitsOne()
    {
        var (app, console, _) = CreateApp();

        var result = await app.RunAsync(["deploy", "unknown-target"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("Unknown deployment target");
    }

    [Test]
    [Arguments("github-pages", DeploymentTarget.GitHubPages)]
    [Arguments("GITHUB-PAGES", DeploymentTarget.GitHubPages)]
    [Arguments("azure-swa", DeploymentTarget.AzureStaticWebApps)]
    [Arguments("AZURE-SWA", DeploymentTarget.AzureStaticWebApps)]
    public async Task DeployCommand_KnownTargets_ParsedCaseInsensitively(string arg, DeploymentTarget expected)
    {
        var (app, _, initializer) = CreateApp();

        var result = await app.RunAsync(["deploy", arg]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(initializer.CapturedTarget).IsEqualTo(expected);
    }

    [Test]
    public async Task DeployCommand_Success_ShowsCreatedLineForEachFile()
    {
        var (app, console, initializer) = CreateApp();
        initializer.ResultFactory = () => new DeploymentInitResult(
            DeploymentTarget.GitHubPages,
            [".github/workflows/deploy.yml", "some/other-file.txt"]);

        var result = await app.RunAsync(["deploy", "github-pages"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Created");
        await Assert.That(console.Output).Contains("deploy.yml");
        await Assert.That(console.Output).Contains("other-file.txt");
    }

    [Test]
    public async Task DeployCommand_InvalidOperationException_ExitsOneWithMessage()
    {
        var (app, console, initializer) = CreateApp();
        initializer.ThrowException = new InvalidOperationException("no git repo");

        var result = await app.RunAsync(["deploy", "github-pages"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("no git repo");
    }

    [Test]
    public async Task DeployCommand_IOException_ExitsOneWithMessage()
    {
        var (app, console, initializer) = CreateApp();
        initializer.ThrowException = new IOException("disk full");

        var result = await app.RunAsync(["deploy", "github-pages"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("disk full");
    }

    [Test]
    public async Task DeployCommand_UnauthorizedAccessException_ExitsOneWithMessage()
    {
        var (app, console, initializer) = CreateApp();
        initializer.ThrowException = new UnauthorizedAccessException("access denied");

        var result = await app.RunAsync(["deploy", "github-pages"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("access denied");
    }

    [Test]
    public async Task DeployCommand_NotSupportedException_ExitsOneWithMessage()
    {
        var (app, console, initializer) = CreateApp();
        initializer.ThrowException = new NotSupportedException("not supported here");

        var result = await app.RunAsync(["deploy", "github-pages"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("not supported here");
    }

    [Test]
    public async Task DeployCommand_ArgumentException_ExitsOneWithMessage()
    {
        var (app, console, initializer) = CreateApp();
        initializer.ThrowException = new ArgumentException("bad target");

        var result = await app.RunAsync(["deploy", "github-pages"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("bad target");
    }
}
