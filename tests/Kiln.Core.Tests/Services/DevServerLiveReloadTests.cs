namespace Kiln.Core.Tests.Services;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Kiln.Abstractions;
using Kiln.Models;
using Kiln.Services;

public class DevServerLiveReloadTests
{
    private const int RebuildCountAfterBurst = 2;
    private const int WaitForDebounceMilliseconds = 450;
    private const int PortForSiteBaseUrl = 5555;
    private const int EventPrefixLength = 6;
    private const int DataPrefixLength = 5;
    private const string LiveReloadEndpoint = "/__kiln/livereload";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    [Test]
    public async Task RunAsync_DebouncesChanges_AndUsesSingleFlightRebuilds()
    {
        var projectDir = CreateWatchedProject();
        var builder = new RecordingSiteBuilder("_site");
        var configLoader = new StubSiteConfigLoader("_site");
        var server = new DevServer(builder, configLoader);
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        var runTask = server.RunAsync(projectDir, port, ct: cts.Token);

        try
        {
            await builder.WaitForBuildCountAsync(1, DefaultTimeout);
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            await WaitForServerReadyAsync(client, port, DefaultTimeout);

            var sourceFile = Path.Combine(projectDir, "content", "posts", "hello.md");
            await File.WriteAllTextAsync(sourceFile, "first", CancellationToken.None);
            await File.WriteAllTextAsync(sourceFile, "second", CancellationToken.None);
            await File.WriteAllTextAsync(sourceFile, "third", CancellationToken.None);

            await builder.WaitForBuildCountAsync(RebuildCountAfterBurst, DefaultTimeout);
            await Task.Delay(TimeSpan.FromMilliseconds(WaitForDebounceMilliseconds));

            await Assert.That(builder.BuildCount).IsEqualTo(RebuildCountAfterBurst);
            await Assert.That(builder.MaxConcurrentBuilds).IsEqualTo(1);
        }
        finally
        {
            await cts.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(3));
            Directory.Delete(projectDir, true);
        }
    }

    [Test]
    public async Task RunAsync_ServesHtmlWithInjectedScript_ButBuildOutputStaysClean()
    {
        var projectDir = CreateRenderableSite();
        var siteBuilder = CreateRealSiteBuilder();
        var configLoader = new SiteConfigLoader();
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        try
        {
            var buildResult = await siteBuilder.BuildAsync(projectDir, false, BuildEnvironment.Development, CancellationToken.None);
            await Assert.That(buildResult.Success).IsTrue();

            var builtHtml = await File.ReadAllTextAsync(Path.Combine(projectDir, "_site", "blog", "hello-world", "index.html"));
            await Assert.That(builtHtml).DoesNotContain(LiveReloadEndpoint);

            var server = new DevServer(siteBuilder, configLoader);
            var runTask = server.RunAsync(projectDir, port, ct: cts.Token);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            await WaitForServerReadyAsync(client, port, DefaultTimeout);
            var servedHtml = await client.GetStringAsync($"http://localhost:{port}/blog/hello-world/");
            await Assert.That(servedHtml).Contains(LiveReloadEndpoint);

            await cts.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Test]
    public async Task RunAsync_SseBroadcastsReloadAndCssEvents()
    {
        var projectDir = CreateWatchedProject();
        var builder = new RecordingSiteBuilder("_site");
        var configLoader = new StubSiteConfigLoader("_site");
        var server = new DevServer(builder, configLoader);
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        var runTask = server.RunAsync(projectDir, port, ct: cts.Token);

        try
        {
            await builder.WaitForBuildCountAsync(1, DefaultTimeout);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            await WaitForServerReadyAsync(client, port, DefaultTimeout);
            using var response = await client.GetAsync(
                $"http://localhost:{port}{LiveReloadEndpoint}",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/event-stream");

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await File.WriteAllTextAsync(Path.Combine(projectDir, "content", "posts", "hello.md"), "content update", CancellationToken.None);
            var firstEvent = await ReadNextSseEventAsync(reader, DefaultTimeout);
            await Assert.That(firstEvent.EventName).IsEqualTo("reload");

            Directory.CreateDirectory(Path.Combine(projectDir, "static"));
            await File.WriteAllTextAsync(Path.Combine(projectDir, "static", "site.css"), "body { color: red; }", CancellationToken.None);
            var secondEvent = await ReadNextSseEventAsync(reader, DefaultTimeout);
            await Assert.That(secondEvent.EventName).IsEqualTo("css");
        }
        finally
        {
            await cts.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(3));
            Directory.Delete(projectDir, true);
        }
    }

    [Test]
    public async Task RunAsync_SseBroadcastsErrorWhenRebuildFails()
    {
        var projectDir = CreateWatchedProject();
        var builder = new RecordingSiteBuilder("_site");
        var configLoader = new StubSiteConfigLoader("_site");
        var server = new DevServer(builder, configLoader);
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        var runTask = server.RunAsync(projectDir, port, ct: cts.Token);

        try
        {
            await builder.WaitForBuildCountAsync(1, DefaultTimeout);
            builder.FailNextBuild("simulated rebuild failure");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            await WaitForServerReadyAsync(client, port, DefaultTimeout);
            using var response = await client.GetAsync(
                $"http://localhost:{port}{LiveReloadEndpoint}",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await File.WriteAllTextAsync(Path.Combine(projectDir, "content", "posts", "hello.md"), "trigger failure", CancellationToken.None);
            var evt = await ReadNextSseEventAsync(reader, DefaultTimeout);

            await Assert.That(evt.EventName).IsEqualTo("error");
            await Assert.That(evt.Data).Contains("simulated rebuild failure");
        }
        finally
        {
            await cts.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(3));
            Directory.Delete(projectDir, true);
        }
    }

    [Test]
    public async Task RunAsync_ShutdownClosesOpenSseStreams()
    {
        var projectDir = CreateWatchedProject();
        var builder = new RecordingSiteBuilder("_site");
        var configLoader = new StubSiteConfigLoader("_site");
        var server = new DevServer(builder, configLoader);
        var port = GetFreePort();
        using var cts = new CancellationTokenSource();

        var runTask = server.RunAsync(projectDir, port, ct: cts.Token);

        try
        {
            await builder.WaitForBuildCountAsync(1, DefaultTimeout);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            await WaitForServerReadyAsync(client, port, DefaultTimeout);
            using var response = await client.GetAsync(
                $"http://localhost:{port}{LiveReloadEndpoint}",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            await cts.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(3));

            var lineTask = reader.ReadLineAsync();
            var completed = await Task.WhenAny(lineTask, Task.Delay(TimeSpan.FromSeconds(2)));
            await Assert.That(completed == lineTask).IsTrue();
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    private static async Task<SseEvent> ReadNextSseEventAsync(StreamReader reader, TimeSpan timeout)
    {
        var eventName = string.Empty;
        var data = string.Empty;

        while (true)
        {
            var line = await reader.ReadLineAsync().WaitAsync(timeout);
            if (line is null)
                throw new IOException("SSE stream closed before event was received.");

            if (line.Length == 0)
            {
                if (eventName.Length > 0)
                    return new SseEvent(eventName, data);
                continue;
            }

            if (line.StartsWith(":", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[EventPrefixLength..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data = line[DataPrefixLength..].TrimStart();
            }
        }
    }

    private static async Task WaitForServerReadyAsync(HttpClient client, int port, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync($"http://localhost:{port}/");
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
                    return;
            }
            catch (HttpRequestException)
            {
                // Listener not yet ready.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        }

        throw new TimeoutException("Dev server did not become ready in time.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static ISiteBuilder CreateRealSiteBuilder()
    {
        var markdownProcessor = new MarkdownProcessor();
        var contentReader = new ContentReader(markdownProcessor);
        var templateRenderer = new TemplateRenderer();
        var permalinkGenerator = new PermalinkGenerator();
        var configLoader = new SiteConfigLoader();
        var pluginLoader = new PluginLoader();
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, []);
    }

    private static string CreateWatchedProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-live-reload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "static"));
        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello.md"), "initial");
        return dir;
    }

    private static string CreateRenderableSite()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-serve-injection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            theme: default
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html><body>{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html><body>Not Found</body></html>");

        return dir;
    }

    private sealed class StubSiteConfigLoader(string outputDir) : ISiteConfigLoader
    {
        public SiteConfiguration Load(string projectPath)
        {
            return new SiteConfiguration
            {
                Title = "Test",
                BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", PortForSiteBaseUrl).Uri,
                OutputDir = outputDir
            };
        }
    }

    private sealed class RecordingSiteBuilder(string outputDir) : ISiteBuilder
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _waiters = new();
        private readonly object _sync = new();
        private string? _nextFailure;
        private int _currentConcurrent;

        public int BuildCount { get; private set; }
        public int MaxConcurrentBuilds { get; private set; }

        public void FailNextBuild(string message)
        {
            lock (_sync)
                _nextFailure = message;
        }

        private const int SimulatedBuildMilliseconds = 120;

        public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default)
            => BuildAsync(projectPath, includeDrafts, BuildEnvironment.Development, ct);

        public async Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, CancellationToken ct)
        {
            var active = Interlocked.Increment(ref _currentConcurrent);
            if (active > MaxConcurrentBuilds)
                MaxConcurrentBuilds = active;

            try
            {
                await Task.Delay(SimulatedBuildMilliseconds, ct);

                string? failure;
                lock (_sync)
                {
                    BuildCount++;
                    failure = _nextFailure;
                    _nextFailure = null;
                    CompleteWaiters(BuildCount);
                }

                var outputPath = Path.Combine(projectPath, outputDir);
                Directory.CreateDirectory(outputPath);
                await File.WriteAllTextAsync(Path.Combine(outputPath, "index.html"), "<html><body>ok</body></html>", ct);

                if (failure is not null)
                {
                    return new BuildResult
                    {
                        TotalFiles = 1,
                        RenderedFiles = 0,
                        SkippedDrafts = 0,
                        Duration = TimeSpan.FromMilliseconds(1),
                        OutputDirectory = outputPath,
                        Errors = [failure]
                    };
                }

                return new BuildResult
                {
                    TotalFiles = 1,
                    RenderedFiles = 1,
                    SkippedDrafts = 0,
                    Duration = TimeSpan.FromMilliseconds(1),
                    OutputDirectory = outputPath
                };
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrent);
            }
        }

        public Task WaitForBuildCountAsync(int expectedCount, TimeSpan timeout)
        {
            lock (_sync)
            {
                if (BuildCount >= expectedCount)
                    return Task.CompletedTask;

                var waiter = _waiters.GetOrAdd(expectedCount,
                    static _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                return waiter.Task.WaitAsync(timeout);
            }
        }

        private void CompleteWaiters(int buildCount)
        {
            foreach (var pair in _waiters)
            {
                if (pair.Key <= buildCount && _waiters.TryRemove(pair.Key, out var waiter))
                    waiter.TrySetResult(true);
            }
        }
    }

    private sealed record SseEvent(string EventName, string Data);
}
