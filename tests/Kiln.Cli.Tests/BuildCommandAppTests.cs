namespace Kiln.Cli.Tests;

using Kiln.Abstractions;
using Kiln.Cli.Commands;
using Kiln.Cli.Tests.Fakes;
using Kiln.Models;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class BuildCommandAppTests
{
    private const int FiveRenderedFiles = 5;

    private static (CommandAppTester App, TestConsole Console, FakeSiteBuilder Builder, FakeSiteConfigLoader ConfigLoader, FakeSearchIndexer Indexer) CreateApp()
    {
        var builder = new FakeSiteBuilder();
        var configLoader = new FakeSiteConfigLoader();
        var indexer = new FakeSearchIndexer();

        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton<ISiteBuilder>(builder);
            services.AddSingleton<ISiteConfigLoader>(configLoader);
            services.AddSingleton<ISearchIndexer>(indexer);
        });

        app.Configure(config => config.AddCommand<BuildCommand>("build"));

        return (app, console, builder, configLoader, indexer);
    }

    [Test]
    public async Task BuildCommand_Success_ExitsZeroAndShowsRenderedFileCount()
    {
        var (app, console, builder, _, _) = CreateApp();
        builder.ResultFactory = () => new BuildResult
        {
            TotalFiles = FiveRenderedFiles,
            RenderedFiles = FiveRenderedFiles,
            SkippedDrafts = 0,
            Duration = TimeSpan.Zero,
            OutputDirectory = "_site",
        };

        var result = await app.RunAsync(["build", "."]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains($"{FiveRenderedFiles} files rendered");
    }

    [Test]
    public async Task BuildCommand_Failure_ExitsOneAndShowsError()
    {
        var (app, console, builder, _, _) = CreateApp();
        builder.ResultFactory = () => new BuildResult
        {
            TotalFiles = 0,
            RenderedFiles = 0,
            SkippedDrafts = 0,
            Duration = TimeSpan.Zero,
            OutputDirectory = "_site",
            Errors = new System.Collections.ObjectModel.Collection<string> { "template not found" },
        };

        var result = await app.RunAsync(["build", "."]);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("template not found");
    }

    [Test]
    public async Task BuildCommand_Drafts_PassesIncludeDraftsTrue()
    {
        var (app, _, builder, _, _) = CreateApp();

        await app.RunAsync(["build", ".", "--drafts"]);

        await Assert.That(builder.CapturedIncludeDrafts).IsTrue();
    }

    [Test]
    [Arguments("--production")]
    [Arguments("--release")]
    public async Task BuildCommand_ProductionFlags_ResolveToProductionEnvironment(string flag)
    {
        var (app, _, builder, _, _) = CreateApp();

        await app.RunAsync(["build", ".", flag]);

        await Assert.That(builder.CapturedEnvironment).IsEqualTo(BuildEnvironment.Production);
    }

    [Test]
    public async Task BuildCommand_EnvironmentOptionProduction_ResolvesToProductionEnvironment()
    {
        var (app, _, builder, _, _) = CreateApp();

        await app.RunAsync(["build", ".", "-e", "production"]);

        await Assert.That(builder.CapturedEnvironment).IsEqualTo(BuildEnvironment.Production);
    }

    [Test]
    public async Task BuildCommand_NoSearch_DoesNotCallSearchIndexerEvenWhenEnabled()
    {
        var (app, _, _, configLoader, indexer) = CreateApp();
        configLoader.Config = new SiteConfiguration
        {
            Title = "Test",
            BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
            Search = new SearchOptions { Enabled = true },
        };

        var result = await app.RunAsync(["build", ".", "--no-search"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(indexer.WasCalled).IsFalse();
    }

    [Test]
    public async Task BuildCommand_SearchEnabledAndSucceeds_ShowsSearchIndexBuiltMessage()
    {
        var (app, console, _, configLoader, indexer) = CreateApp();
        configLoader.Config = new SiteConfiguration
        {
            Title = "Test",
            BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
            Search = new SearchOptions { Enabled = true },
        };
        indexer.ResultFactory = () => new SearchIndexResult(true, [], []);

        var result = await app.RunAsync(["build", "."]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(indexer.WasCalled).IsTrue();
        await Assert.That(console.Output).Contains("Search index built.");
    }

    [Test]
    public async Task BuildCommand_SearchEnabledAndFails_ShowsSearchWarningButStillExitsZero()
    {
        var (app, console, _, configLoader, indexer) = CreateApp();
        configLoader.Config = new SiteConfiguration
        {
            Title = "Test",
            BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555).Uri,
            Search = new SearchOptions { Enabled = true },
        };
        indexer.ResultFactory = () => new SearchIndexResult(false, [], ["pagefind binary missing"]);

        var result = await app.RunAsync(["build", "."]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Search index failed");
        await Assert.That(console.Output).Contains("pagefind binary missing");
    }
}
