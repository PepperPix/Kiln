namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class DeployCommandForceTests
{
    [Test]
    public async Task DeployCommand_WithoutForce_PassesForceFalse()
    {
        var (app, _, initializer) = CreateApp();

        var result = await app.RunAsync(["deploy", "github-pages"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(initializer.CapturedOptions).IsNotNull();
        await Assert.That(initializer.CapturedOptions!.Force).IsFalse();
    }

    [Test]
    public async Task DeployCommand_WithForce_PassesForceTrue()
    {
        var (app, _, initializer) = CreateApp();

        var result = await app.RunAsync(["deploy", "github-pages", "--force"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(initializer.CapturedOptions!.Force).IsTrue();
    }

    [Test]
    public async Task DeployCommand_SkippedFiles_AreReportedWithExitCodeZero()
    {
        var (app, console, initializer) = CreateApp();
        initializer.Result = new DeploymentInitResult(DeploymentTarget.AzureStaticWebApps, ["staticwebapp.config.json"])
        {
            SkippedFiles = [".github/workflows/azure-swa.yml"],
        };

        var result = await app.RunAsync(["deploy", "azure-swa"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Created");
        await Assert.That(console.Output).Contains("staticwebapp.config.json");
        await Assert.That(console.Output).Contains("Skipped (exists)");
        await Assert.That(console.Output).Contains("azure-swa.yml");
        await Assert.That(console.Output).Contains("--force");
    }

    [Test]
    public async Task DeployCommand_InitializerWithoutOptionsOverload_StillWorksThroughDefaultMember()
    {
        var fake = new FakeDeploymentInitializer();
        var (app, _) = CommandAppTesterFactory.Create(services => services.AddSingleton<IDeploymentInitializer>(fake));
        app.Configure(config => config.AddCommand<DeployCommand>("deploy"));

        var result = await app.RunAsync(["deploy", "azure-swa", "--force"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(fake.CapturedTarget).IsEqualTo(DeploymentTarget.AzureStaticWebApps);
    }

    private static (CommandAppTester App, TestConsole Console, RecordingDeploymentInitializer Initializer) CreateApp()
    {
        var initializer = new RecordingDeploymentInitializer();

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<IDeploymentInitializer>(initializer);
        });

        app.Configure(config => config.AddCommand<DeployCommand>("deploy"));

        return (app, console, initializer);
    }
}
