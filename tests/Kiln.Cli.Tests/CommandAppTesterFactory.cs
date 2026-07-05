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
    public static (CommandAppTester App, TestConsole Console) Create(Action<IServiceCollection> configureServices)
    {
        var console = new TestConsole();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        configureServices(services);

        var registrar = new TypeRegistrar(services);
        var app = new CommandAppTester(registrar, console: console);

        return (app, console);
    }
}
