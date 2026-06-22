namespace Kiln.DependencyInjection;

using Kiln.Abstractions;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;

public static class KilnServiceCollectionExtensions
{
    public static IKilnBuilder AddKiln(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMarkdownProcessor, MarkdownProcessor>();
        services.AddSingleton<IContentReader, ContentReader>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddSingleton<IPermalinkGenerator, PermalinkGenerator>();
        services.AddSingleton<ISiteConfigLoader, SiteConfigLoader>();
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<ISiteBuilder, SiteBuilder>();
        services.AddSingleton<IDevServer, DevServer>();
        services.AddSingleton<IAssetMinifier, NuglifyAssetMinifier>();
        services.AddSingleton<IAssetMinifier, NoOpAssetMinifier>();

        return new KilnBuilder(services);
    }

    private sealed class KilnBuilder(IServiceCollection services) : IKilnBuilder
    {
        private readonly IServiceCollection _services = services;

        public IKilnBuilder UseMinifier<T>() where T : class, IAssetMinifier
        {
            for (var index = _services.Count - 1; index >= 0; index--)
            {
                if (_services[index].ServiceType == typeof(IAssetMinifier))
                {
                    _services.RemoveAt(index);
                }
            }

            _services.AddSingleton<IAssetMinifier, T>();
            return this;
        }

        public IKilnBuilder AddAssetProcessor<T>() where T : class, IAssetProcessor
        {
            _services.AddSingleton<IAssetProcessor, T>();
            return this;
        }
    }
}
