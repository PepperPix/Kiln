namespace Kiln.Cli.Tests;

using System.Net;
using Kiln.Cli.Commands;
using Kiln.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Reads and writes the process-wide <c>KILN_PAGEFIND_PATH</c> variable, so it must not run in parallel.
/// </summary>
[NotInParallel]
public class SearchIndexCommandBinaryPathTests
{
    private string? _savedPagefindPathOverride;

    [Before(Test)]
    public void ClearPagefindPathOverride()
    {
        _savedPagefindPathOverride = Environment.GetEnvironmentVariable("KILN_PAGEFIND_PATH");
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", null);
    }

    [After(Test)]
    public void RestorePagefindPathOverride()
    {
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", _savedPagefindPathOverride);
    }

    [Test]
    public async Task SearchIndexCommand_BinaryPathMissing_PrintsErrorExitsOneAndDoesNotDownload()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-cli-binarypath-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, "_site"));
        await File.WriteAllTextAsync(Path.Combine(projectDir, "site.yaml"), """
            title: Test
            baseUrl: http://localhost
            search:
              enabled: true
              binaryPath: tools/does-not-exist
            """);

        var http = new CountingHttpHandler();
        try
        {
            IPagefindBinaryProvider provider = new PagefindBinaryProvider(
                Path.Combine(projectDir, "cache"),
                http,
                pathOverride: string.Empty,
                processRunner: null);
            var (app, console) = CommandAppTesterFactory.Create(services =>
            {
                services.AddSingleton<ISiteConfigLoader, SiteConfigLoader>();
                services.AddSingleton<IProcessRunner, SystemProcessRunner>();
                services.AddSingleton(provider);
                services.AddSingleton<ISearchIndexer, PagefindSearchIndexer>();
            });
            app.Configure(config => config.AddCommand<SearchIndexCommand>("search-index"));

            var result = await app.RunAsync(["search-index", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("ERROR:");
            await Assert.That(console.Output).Contains("search.binaryPath");
            await Assert.That(console.Output).Contains(Path.Combine(projectDir, "tools", "does-not-exist"));
            await Assert.That(http.CallCount).IsEqualTo(0);
        }
        finally
        {
            http.Dispose();
            Directory.Delete(projectDir, true);
        }
    }

    private sealed class CountingHttpHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
