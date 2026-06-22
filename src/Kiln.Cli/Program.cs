using Kiln.Cli.Commands;
using Kiln.Cli.Infrastructure;
using Kiln.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddKiln();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("kiln");
    config.SetApplicationVersion("0.1.0");

    config.AddCommand<BuildCommand>("build")
        .WithDescription("Build the static site.");

    config.AddCommand<ServeCommand>("serve")
        .WithDescription("Start a local dev server with auto-rebuild.");

    config.AddCommand<NewCommand>("new")
        .WithDescription("Create a new site project.");

    config.AddCommand<DeployCommand>("deploy")
        .WithDescription("Initialize CI/CD deployment for various targets.");
});

return await app.RunAsync(args).ConfigureAwait(false);
