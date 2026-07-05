namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class NewCommandAppTests
{
    private static (CommandAppTester App, TestConsole Console, FakeScaffolder Scaffolder) CreateApp()
    {
        var scaffolder = new FakeScaffolder();

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<IScaffolder>(scaffolder);
        });

        app.Configure(config => config.AddCommand<NewCommand>("new"));

        return (app, console, scaffolder);
    }

    [Test]
    public async Task NewCommand_Success_ExitsZeroAndShowsCreatedAndNextSteps()
    {
        var (app, console, scaffolder) = CreateApp();
        scaffolder.ResultFactory = () => new ScaffoldResult("/tmp/mysite", []);

        var result = await app.RunAsync(["new", "mysite"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Created");
        await Assert.That(console.Output).Contains("Next steps");
    }

    [Test]
    public async Task NewCommand_InvalidOperationException_ExitsOneWithMessage()
    {
        var (app, console, scaffolder) = CreateApp();
        scaffolder.ThrowException = new InvalidOperationException("already exists");

        var result = await app.RunAsync(["new", "mysite"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("already exists");
    }

    [Test]
    public async Task NewCommand_IOException_ExitsOneWithMessage()
    {
        var (app, console, scaffolder) = CreateApp();
        scaffolder.ThrowException = new IOException("disk full");

        var result = await app.RunAsync(["new", "mysite"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("disk full");
    }

    [Test]
    public async Task NewCommand_UnauthorizedAccessException_ExitsOneWithMessage()
    {
        var (app, console, scaffolder) = CreateApp();
        scaffolder.ThrowException = new UnauthorizedAccessException("access denied");

        var result = await app.RunAsync(["new", "mysite"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("access denied");
    }

    [Test]
    public async Task NewCommand_NotSupportedException_ExitsOneWithMessage()
    {
        var (app, console, scaffolder) = CreateApp();
        scaffolder.ThrowException = new NotSupportedException("not supported here");

        var result = await app.RunAsync(["new", "mysite"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("not supported here");
    }

    [Test]
    public async Task NewCommand_ArgumentException_ExitsOneWithMessage()
    {
        var (app, console, scaffolder) = CreateApp();
        scaffolder.ThrowException = new ArgumentException("bad name");

        var result = await app.RunAsync(["new", "mysite"]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("bad name");
    }
}
