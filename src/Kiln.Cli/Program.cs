using Kiln.Cli.Commands;
using Kiln.Cli.Infrastructure;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddSingleton<IMarkdownProcessor, MarkdownProcessor>();
services.AddSingleton<IContentReader, ContentReader>();
services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
services.AddSingleton<ISiteConfigLoader, SiteConfigLoader>();
services.AddSingleton<ISiteBuilder, SiteBuilder>();
services.AddSingleton<IDevServer, DevServer>();

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
});

return await app.RunAsync(args).ConfigureAwait(false);
