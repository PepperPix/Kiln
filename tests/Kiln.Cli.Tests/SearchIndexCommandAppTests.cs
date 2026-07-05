namespace Kiln.Cli.Tests;

using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class SearchIndexCommandAppTests
{
    private static (CommandAppTester App, TestConsole Console, FakeSearchIndexer Indexer, FakeSiteConfigLoader ConfigLoader) CreateApp()
    {
        var indexer = new FakeSearchIndexer();
        var configLoader = new FakeSiteConfigLoader();

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<ISearchIndexer>(indexer);
            services.AddSingleton<ISiteConfigLoader>(configLoader);
        });

        app.Configure(config => config.AddCommand<SearchIndexCommand>("search-index"));

        return (app, console, indexer, configLoader);
    }

    [Test]
    public async Task SearchIndexCommand_ExtendedFlag_PassesExtendedTrue()
    {
        var (app, _, indexer, _) = CreateApp();

        await app.RunAsync(["search-index", ".", "--extended"]);

        await Assert.That(indexer.CapturedOptions?.Extended).IsTrue();
    }

    [Test]
    public async Task SearchIndexCommand_ConfigExtended_PassesExtendedTrueWithoutFlag()
    {
        var (app, _, indexer, configLoader) = CreateApp();
        configLoader.Config = new SiteConfiguration
        {
            Title = "Test",
            BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
            Search = new SearchOptions { Extended = true },
        };

        await app.RunAsync(["search-index", "."]);

        await Assert.That(indexer.CapturedOptions?.Extended).IsTrue();
    }

    [Test]
    public async Task SearchIndexCommand_NoDownload_PassesAllowDownloadFalse()
    {
        var (app, _, indexer, _) = CreateApp();

        await app.RunAsync(["search-index", ".", "--no-download"]);

        await Assert.That(indexer.CapturedAllowDownload).IsFalse();
    }

    [Test]
    public async Task SearchIndexCommand_WithoutNoDownload_PassesAllowDownloadTrue()
    {
        var (app, _, indexer, _) = CreateApp();

        await app.RunAsync(["search-index", "."]);

        await Assert.That(indexer.CapturedAllowDownload).IsTrue();
    }

    [Test]
    public async Task SearchIndexCommand_Failure_ExitsOneAndShowsErrors()
    {
        var (app, console, indexer, _) = CreateApp();
        indexer.ResultFactory = () => new SearchIndexResult(false, [], ["pagefind binary not found"]);

        var result = await app.RunAsync(["search-index", "."]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("pagefind binary not found");
    }

    [Test]
    public async Task SearchIndexCommand_Success_ExitsZeroAndShowsIndexPath()
    {
        var (app, console, indexer, _) = CreateApp();
        indexer.ResultFactory = () => new SearchIndexResult(true, [], []);

        var result = await app.RunAsync(["search-index", "."]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Search index built.");
        await Assert.That(console.Output).Contains("pagefind");
    }
}
