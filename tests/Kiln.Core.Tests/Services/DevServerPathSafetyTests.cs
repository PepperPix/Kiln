namespace Kiln.Core.Tests.Services;

using System.Net;
using System.Net.Sockets;
using System.Text;
using Kiln.Abstractions;
using Kiln.Models;
using Kiln.Services;

// Same free-port race mitigation as DevServerLiveReloadTests: GetFreePort() is check-then-use.
[Retry(PortRaceRetryAttempts, RetryOnExceptionTypes = new[] { typeof(HttpListenerException) })]
public class DevServerPathSafetyTests
{
    private const int PortRaceRetryAttempts = 2;
    private const int PortForSiteBaseUrl = 5555;
    private const int HttpOk = 200;
    private const string ProjectSecret = "PROJECT-SECRET-CONTENT";
    private const string OuterSecret = "OUTER-SECRET-CONTENT";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);
    private static readonly HttpClient ReadinessClient = new() { Timeout = TimeSpan.FromSeconds(4) };

    [Test]
    [Arguments("/..%2fsecret.txt")]
    [Arguments("/..%2f..%2fouter-secret.txt")]
    [Arguments("/%2e%2e%2fsecret.txt")]
    [Arguments("/%2e%2e/secret.txt")]
    [Arguments("/..%5csecret.txt")]
    [Arguments("/../secret.txt")]
    [Arguments("/nested/..%2f..%2fsecret.txt")]
    public async Task Request_WithTraversalTarget_NeverServesFilesOutsideOutputDir(string target)
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await SendRawAsync(fixture.Port, target, $"localhost:{fixture.Port}");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpOk);
        await Assert.That(response.Body).DoesNotContain(ProjectSecret);
        await Assert.That(response.Body).DoesNotContain(OuterSecret);
    }

    [Test]
    [Arguments("/index.html")]
    [Arguments("/")]
    [Arguments("/nested/")]
    public async Task Request_ForFilesInsideOutputDir_ReturnsContent(string target)
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await SendRawAsync(fixture.Port, target, $"localhost:{fixture.Port}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpOk);
        await Assert.That(response.Body).Contains("served-ok");
    }

    [Test]
    public async Task Request_WithForeignHostHeader_IsRejected()
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await SendRawAsync(fixture.Port, "/index.html", "evil.example.com");

        await Assert.That(response.StatusCode).IsNotEqualTo(HttpOk);
        await Assert.That(response.Body).DoesNotContain("served-ok");
    }

    [Test]
    [Arguments("localhost")]
    [Arguments("LOCALHOST")]
    public async Task Request_WithLocalhostHostHeader_IsServed(string host)
    {
        await using var fixture = await ServerFixture.StartAsync();

        var response = await SendRawAsync(fixture.Port, "/index.html", $"{host}:{fixture.Port}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpOk);
    }

    private static async Task<RawResponse> SendRawAsync(int port, string target, string hostHeader)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("localhost", port);
        await using var stream = client.GetStream();

        var request = $"GET {target} HTTP/1.1\r\nHost: {hostHeader}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));

        using var memory = new MemoryStream();
        using var readCts = new CancellationTokenSource(DefaultTimeout);
        try
        {
            await stream.CopyToAsync(memory, readCts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            // Connection reset or timeout after the response was sent; evaluate what was received.
        }

        var text = Encoding.UTF8.GetString(memory.ToArray());
        var statusLine = text.Split("\r\n", 2)[0];
        var parts = statusLine.Split(' ', 3);
        var status = parts.Length >= 2 && int.TryParse(parts[1], out var code) ? code : 0;
        var bodyStart = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var body = bodyStart >= 0 ? text[(bodyStart + 4)..] : string.Empty;
        return new RawResponse(status, body);
    }

    private sealed record RawResponse(int StatusCode, string Body);

    private sealed class ServerFixture : IAsyncDisposable
    {
        private readonly string _containerDir;
        private readonly CancellationTokenSource _cts = new();
        private Task _runTask = Task.CompletedTask;

        private ServerFixture(string containerDir, int port)
        {
            _containerDir = containerDir;
            Port = port;
        }

        public int Port { get; }

        public static async Task<ServerFixture> StartAsync()
        {
            var container = Path.Combine(Path.GetTempPath(), $"kiln-devserver-safety-{Guid.NewGuid():N}");
            var project = Path.Combine(container, "project");
            Directory.CreateDirectory(Path.Combine(project, "_site", "nested"));
            await File.WriteAllTextAsync(Path.Combine(project, "_site", "index.html"), "<html><body>served-ok</body></html>");
            await File.WriteAllTextAsync(Path.Combine(project, "_site", "nested", "index.html"), "<html><body>served-ok</body></html>");
            await File.WriteAllTextAsync(Path.Combine(project, "secret.txt"), ProjectSecret);
            await File.WriteAllTextAsync(Path.Combine(container, "outer-secret.txt"), OuterSecret);

            var fixture = new ServerFixture(container, GetFreePort());
            var server = new DevServer(new StubSiteBuilder(), new StubSiteConfigLoader(), new NoOpSearchIndexer());
            fixture._runTask = server.RunAsync(project, fixture.Port, ct: fixture._cts.Token);
            await fixture.WaitUntilReadyAsync();
            return fixture;
        }

        private static int GetFreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            try
            {
                await _runTask.WaitAsync(TimeSpan.FromSeconds(3));
            }
            finally
            {
                _cts.Dispose();
                Directory.Delete(_containerDir, recursive: true);
            }
        }

        private async Task WaitUntilReadyAsync()
        {
            var deadline = DateTimeOffset.UtcNow + DefaultTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    using var response = await ReadinessClient.GetAsync($"http://localhost:{Port}/");
                    return;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
                }
            }

            throw new TimeoutException("Dev server did not become ready in time.");
        }
    }

    private sealed class StubSiteConfigLoader : ISiteConfigLoader
    {
        public SiteConfiguration Load(string projectPath) => new()
        {
            Title = "Test",
            BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", PortForSiteBaseUrl).Uri,
            OutputDir = "_site",
        };
    }

    private sealed class NoOpSearchIndexer : ISearchIndexer
    {
        public Task<SearchIndexResult> IndexAsync(string outputDir, SearchOptions options, bool allowDownload, CancellationToken ct)
            => Task.FromResult(new SearchIndexResult(true, [], []));
    }

    private sealed class StubSiteBuilder : ISiteBuilder
    {
        public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default)
            => Result(projectPath);

        public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, CancellationToken ct)
            => Result(projectPath);

        public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, IProgress<BuildProgress>? progress, CancellationToken ct)
            => Result(projectPath);

        public Task<BuildResult> BuildAsync(
            string projectPath,
            bool includeDrafts,
            BuildEnvironment environment,
            IProgress<BuildProgress>? progress,
            Uri? baseUrlOverride,
            CancellationToken ct = default)
            => Result(projectPath);

        private static Task<BuildResult> Result(string projectPath) => Task.FromResult(new BuildResult
        {
            TotalFiles = 1,
            RenderedFiles = 1,
            SkippedDrafts = 0,
            Duration = TimeSpan.FromMilliseconds(1),
            OutputDirectory = Path.Combine(projectPath, "_site"),
        });
    }
}
