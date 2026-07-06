namespace Kiln.Cli.Tests;

using Kiln.Cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

/// <summary>
/// Wires up a <see cref="CommandAppTester"/> so that the same <see cref="TestConsole"/> instance
/// is used both for constructor injection into commands (via <see cref="IAnsiConsole"/>) and for
/// <see cref="CommandAppResult.Output"/> capture.
/// </summary>
internal static class CommandAppTesterFactory
{
    // CI runners use much longer absolute paths (e.g. /home/runner/work/Kiln/Kiln/...) than
    // local dev machines. Spectre.Console wraps markup output to the console width (default 80),
    // which can split a path substring mid-word across a line break and break `.Contains(...)`
    // assertions. A very wide width disables wrapping in tests.
    private const int UnwrappedConsoleWidth = 4096;

    public static (CommandAppTester App, TestConsole Console) Create(Action<IServiceCollection> configureServices)
    {
        var console = new TestConsole();
        console.Profile.Width = UnwrappedConsoleWidth;

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        configureServices(services);

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar, console: console);

        return (app, console);
    }
}
