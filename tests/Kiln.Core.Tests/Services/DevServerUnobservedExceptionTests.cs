namespace Kiln.Core.Tests.Services;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Kiln.Abstractions;
using Kiln.Models;
using Kiln.Services;

// Same port check-then-use race as DevServerLiveReloadTests; retried only for that exact exception type.
[Retry(PortRaceRetryAttempts, RetryOnExceptionTypes = new[] { typeof(HttpListenerException) })]
public class DevServerUnobservedExceptionTests
{
    private const int PortRaceRetryAttempts = 2;
    private const int PortForSiteBaseUrl = 5555;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(600);

    [Test]
    public async Task RunAsync_RebuildStillRunningAtShutdown_LeavesNoUnobservedException()
    {
        var unobserved = new ConcurrentQueue<Exception>();

        // Only exceptions that originate in DevServer count; other tests run in the same process.
        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            foreach (var inner in e.Exception.Flatten().InnerExceptions)
            {
                if (inner.StackTrace?.Contains(nameof(DevServer), StringComparison.Ordinal) == true)
                    unobserved.Enqueue(inner);
            }
        }

        TaskScheduler.UnobservedTaskException += OnUnobserved;
        var projectDir = Path.Combine(Path.GetTempPath(), $"kiln-devserver-unobserved-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, "content"));
        try
        {
            var builder = new GatedSiteBuilder();
            var server = new DevServer(builder, new StubSiteConfigLoader(), new NoOpSearchIndexer());
            using var cts = new CancellationTokenSource();

            var runTask = server.RunAsync(projectDir, GetFreePort(), ct: cts.Token);
            await builder.InitialBuildDone.WaitAsync(DefaultTimeout);

            // Give the file watcher a moment to start, then trigger a rebuild that blocks inside the builder.
            await Task.Delay(SettleTime);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "content", "hello.md"), "changed", CancellationToken.None);
            await builder.RebuildStarted.WaitAsync(DefaultTimeout);

            await cts.CancelAsync();
            await runTask.WaitAsync(DefaultTimeout);

            // RunAsync has returned and disposed the semaphore/timer; now let the rebuild finish.
            builder.ReleaseRebuild();
            await Task.Delay(SettleTime);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();

            await Assert.That(unobserved.Select(e => e.GetType().Name).ToArray()).IsEmpty();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
            Directory.Delete(projectDir, true);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
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

    // First build returns at once; every later build blocks (ignoring cancellation) until ReleaseRebuild().
    private sealed class GatedSiteBuilder : ISiteBuilder
    {
        private readonly TaskCompletionSource _initialBuildDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _rebuildStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _rebuildGate = new(0);
        private int _calls;

        public Task InitialBuildDone => _initialBuildDone.Task;

        public Task RebuildStarted => _rebuildStarted.Task;

        public void ReleaseRebuild() => _rebuildGate.Release();

        public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default)
            => RunAsync(projectPath);

        public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, CancellationToken ct)
            => RunAsync(projectPath);

        public Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts, BuildEnvironment environment, IProgress<BuildProgress>? progress, CancellationToken ct)
            => RunAsync(projectPath);

        public Task<BuildResult> BuildAsync(
            string projectPath,
            bool includeDrafts,
            BuildEnvironment environment,
            IProgress<BuildProgress>? progress,
            Uri? baseUrlOverride,
            CancellationToken ct = default)
            => RunAsync(projectPath);

        private async Task<BuildResult> RunAsync(string projectPath)
        {
            var outputPath = Path.Combine(projectPath, "_site");
            Directory.CreateDirectory(outputPath);

            if (Interlocked.Increment(ref _calls) == 1)
            {
                _initialBuildDone.TrySetResult();
            }
            else
            {
                _rebuildStarted.TrySetResult();
                await _rebuildGate.WaitAsync().ConfigureAwait(false);
            }

            return new BuildResult
            {
                TotalFiles = 1,
                RenderedFiles = 1,
                SkippedDrafts = 0,
                Duration = TimeSpan.FromMilliseconds(1),
                OutputDirectory = outputPath,
            };
        }
    }
}
