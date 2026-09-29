namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

internal static class PluginTestApp
{
    public static (CommandAppTester App, TestConsole Console) Create(INuGetPluginClient client, IPluginLockFile lockFile)
    {
        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton(client);
            services.AddSingleton(lockFile);
            services.AddSingleton<IPluginLoader, PluginLoader>();
        });

        app.Configure(config =>
        {
            config.AddBranch("plugin", p =>
            {
                p.AddCommand<PluginSearchCommand>("search");
                p.AddCommand<PluginInfoCommand>("info");
                p.AddCommand<PluginAddCommand>("add");
                p.AddCommand<PluginUpdateCommand>("update");
                p.AddCommand<PluginRemoveCommand>("remove");
                p.AddCommand<PluginListCommand>("list");
            });
        });

        return (app, console);
    }
}
