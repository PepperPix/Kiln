using System.Reflection;
using Kiln.Cli.Commands;
using Kiln.Cli.Infrastructure;
using Kiln.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddKiln();
services.AddSingleton(AnsiConsole.Console);

var appVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    ?? typeof(Program).Assembly.GetName().Version?.ToString()
    ?? "0.0.0";

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("kiln");
    config.SetApplicationVersion(appVersion);

    config.AddCommand<BuildCommand>("build")
        .WithDescription("Build the static site.");

    config.AddCommand<ServeCommand>("serve")
        .WithDescription("Start a local dev server with auto-rebuild.");

    config.AddCommand<NewCommand>("new")
        .WithDescription("Create a new site project.");

    config.AddCommand<DeployCommand>("deploy")
        .WithDescription("Initialize CI/CD deployment for various targets.");

    config.AddBranch("gen", g =>
    {
        g.AddCommand<GenDocsCommand>("docs")
            .WithDescription("Generate reference docs from an OpenAPI spec.");
        g.AddCommand<GenDotNetXmlCommand>("dotnet-xml")
            .WithDescription("Generate reference docs from a .NET XML documentation file.");
    });

    config.AddBranch("search", s =>
    {
        s.AddCommand<SearchIndexCommand>("index")
            .WithDescription("Build the Pagefind search index over the built site.");
    });

    config.AddBranch("plugin", p =>
    {
        p.AddCommand<PluginSearchCommand>("search")
            .WithDescription("Search public NuGet packages tagged with kiln-plugin.");
        p.AddCommand<PluginAddCommand>("add")
            .WithDescription("Download and install a NuGet plugin into the project.");
        p.AddCommand<PluginUpdateCommand>("update")
            .WithDescription("Update a locked plugin or all locked plugins.");
        p.AddCommand<PluginRemoveCommand>("remove")
            .WithDescription("Remove a plugin from the project and its lock entry.");
        p.AddCommand<PluginListCommand>("list")
            .WithDescription("List local plugins with their NuGet lock metadata.");
    });
});

return await app.RunAsync(args).ConfigureAwait(false);
