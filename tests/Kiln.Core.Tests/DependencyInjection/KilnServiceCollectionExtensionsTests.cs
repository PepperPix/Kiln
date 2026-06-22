namespace Kiln.Core.Tests.DependencyInjection;

using Kiln.Abstractions;
using Kiln.DependencyInjection;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;

public class KilnServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddKiln_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddKiln();

        using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<IMarkdownProcessor>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IContentReader>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<ITemplateRenderer>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IPermalinkGenerator>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<ISiteConfigLoader>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IPluginLoader>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<ISiteBuilder>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IDevServer>()).IsNotNull();
    }

    [Test]
    public async Task AddKiln_UsesNoOpAssetMinifierByDefault()
    {
        var services = new ServiceCollection();
        services.AddKiln();

        using var provider = services.BuildServiceProvider();
        var minifier = provider.GetRequiredService<IAssetMinifier>();

        await Assert.That(minifier.GetType()).IsEqualTo(typeof(NoOpAssetMinifier));
        await Assert.That(minifier.Id).IsEqualTo("noop");
        await Assert.That(minifier.Minify("body { color: red; }", AssetType.Css)).IsEqualTo("body { color: red; }");
    }

    [Test]
    public async Task UseMinifier_OverridesDefaultMinifier()
    {
        var services = new ServiceCollection();
        services.AddKiln().UseMinifier<FakeMinifier>();

        using var provider = services.BuildServiceProvider();
        var minifier = provider.GetRequiredService<IAssetMinifier>();

        await Assert.That(minifier.GetType()).IsEqualTo(typeof(FakeMinifier));
        await Assert.That(minifier.Id).IsEqualTo("fake");
    }

    [Test]
    public async Task AddAssetProcessor_RegistersProcessor()
    {
        var services = new ServiceCollection();
        services.AddKiln().AddAssetProcessor<FakeProcessor>();

        using var provider = services.BuildServiceProvider();
        var processors = provider.GetServices<IAssetProcessor>().ToList();

        await Assert.That(processors).Count().IsEqualTo(1);
        await Assert.That(processors[0].GetType()).IsEqualTo(typeof(FakeProcessor));
    }

    [Test]
    public async Task AssetContracts_ExposeValues()
    {
        var asset = new Asset("assets/site.css", AssetType.Css, ReadOnlyMemory<byte>.Empty);
        var context = new AssetContext(asset, "_site");

        await Assert.That(context.Asset.RelativePath).IsEqualTo("assets/site.css");
        await Assert.That(context.Asset.Type).IsEqualTo(AssetType.Css);
        await Assert.That(context.Asset.Content.IsEmpty).IsTrue();
        await Assert.That(context.OutputDir).IsEqualTo("_site");
    }

    private sealed class FakeMinifier : IAssetMinifier
    {
        public string Id => "fake";

        public bool CanMinify(AssetType type) => true;

        public string Minify(string content, AssetType type) => content.Trim();
    }

    private sealed class FakeProcessor : IAssetProcessor
    {
        private const int DefaultOrder = 100;

        public int Order => DefaultOrder;

        public Asset Process(AssetContext context) => context.Asset;
    }
}
