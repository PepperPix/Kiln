namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class PagefindSearchIndexerTests
{
    [Test]
    public async Task IndexAsync_SuccessfulRun_ReturnsSuccess()
    {
        var provider = new FakeBinaryProvider("/fake/pagefind");
        var runner = new FakeProcessRunner(new ProcessRunResult(0, "Done.", string.Empty));
        var indexer = new PagefindSearchIndexer(provider, runner);

        var result = await indexer.IndexAsync(
            "/some/site",
            new SearchOptions { Enabled = true },
            allowDownload: false,
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task IndexAsync_NonZeroExitCode_ReturnsError()
    {
        var provider = new FakeBinaryProvider("/fake/pagefind");
        var runner = new FakeProcessRunner(new ProcessRunResult(1, string.Empty, "indexing failed"));
        var indexer = new PagefindSearchIndexer(provider, runner);

        var result = await indexer.IndexAsync(
            "/some/site",
            new SearchOptions { Enabled = true },
            allowDownload: false,
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("indexing failed");
    }

    [Test]
    public async Task IndexAsync_FallsBackToStdOutWhenStdErrEmpty()
    {
        var provider = new FakeBinaryProvider("/fake/pagefind");
        var runner = new FakeProcessRunner(new ProcessRunResult(2, "stdout error detail", string.Empty));
        var indexer = new PagefindSearchIndexer(provider, runner);

        var result = await indexer.IndexAsync(
            "/some/site",
            new SearchOptions { Enabled = true },
            allowDownload: false,
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors[0]).Contains("stdout error detail");
    }

    [Test]
    public async Task IndexAsync_PassesCorrectSiteArgument()
    {
        var provider = new FakeBinaryProvider("/fake/pagefind");
        var runner = new RecordingProcessRunner(new ProcessRunResult(0, string.Empty, string.Empty));
        var indexer = new PagefindSearchIndexer(provider, runner);

        await indexer.IndexAsync(
            "/my/output/dir",
            new SearchOptions { Enabled = true },
            allowDownload: false,
            CancellationToken.None);

        await Assert.That(runner.LastArguments).Contains("--site");
        await Assert.That(runner.LastArguments).Contains("/my/output/dir");
    }

    [Test]
    public async Task IndexAsync_ProviderThrows_ReturnsError()
    {
        var provider = new ThrowingBinaryProvider("binary not found");
        var runner = new FakeProcessRunner(new ProcessRunResult(0, string.Empty, string.Empty));
        var indexer = new PagefindSearchIndexer(provider, runner);

        var result = await indexer.IndexAsync(
            "/some/site",
            new SearchOptions { Enabled = true },
            allowDownload: false,
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Errors[0]).Contains("binary not found");
    }

    private sealed class FakeBinaryProvider(string path) : IPagefindBinaryProvider
    {
        public Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, CancellationToken ct)
            => Task.FromResult(path);
    }

    private sealed class ThrowingBinaryProvider(string message) : IPagefindBinaryProvider
    {
        public Task<string> GetBinaryPathAsync(bool extended, bool allowDownload, CancellationToken ct)
            => throw new InvalidOperationException(message);
    }

    private sealed class FakeProcessRunner(ProcessRunResult result) : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(string fileName, string arguments, string? workingDirectory, CancellationToken ct)
            => Task.FromResult(result);
    }

    private sealed class RecordingProcessRunner(ProcessRunResult result) : IProcessRunner
    {
        public string LastArguments { get; private set; } = string.Empty;

        public Task<ProcessRunResult> RunAsync(string fileName, string arguments, string? workingDirectory, CancellationToken ct)
        {
            LastArguments = arguments;
            return Task.FromResult(result);
        }
    }
}
