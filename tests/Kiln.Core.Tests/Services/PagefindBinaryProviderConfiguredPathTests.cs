namespace Kiln.Core.Tests.Services;

using System.Net;
using System.Runtime.InteropServices;
using Kiln.Services;

/// <summary>
/// Reads and writes the process-wide <c>KILN_PAGEFIND_PATH</c> variable, so it must not run in parallel
/// with the other Pagefind provider tests (see <see cref="PagefindBinaryProviderTests"/>).
/// </summary>
[NotInParallel]
public class PagefindBinaryProviderConfiguredPathTests
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
    public async Task GetBinaryPath_ConfiguredPathExists_ReturnsItWithoutVersionCheckOrDownload()
    {
        using var env = new TestEnvironment();
        var provider = env.CreateProvider(withPathBinary: false);

        var path = await provider.GetBinaryPathAsync(
            extended: false,
            allowDownload: true,
            env.ConfiguredBinaryPath,
            CancellationToken.None);

        await Assert.That(path).IsEqualTo(env.ConfiguredBinaryPath);
        await Assert.That(env.Runner.CallCount).IsEqualTo(0);
        await Assert.That(env.Http.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetBinaryPath_EnvOverrideExists_BeatsConfiguredPath()
    {
        using var env = new TestEnvironment();
        var provider = env.CreateProvider(withPathBinary: true);
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", env.EnvBinaryPath);

        var path = await provider.GetBinaryPathAsync(
            extended: false,
            allowDownload: false,
            env.ConfiguredBinaryPath,
            CancellationToken.None);

        await Assert.That(path).IsEqualTo(env.EnvBinaryPath);
    }

    [Test]
    public async Task GetBinaryPath_ConfiguredPathExists_BeatsPathBinary()
    {
        using var env = new TestEnvironment();
        var provider = env.CreateProvider(withPathBinary: true);

        var path = await provider.GetBinaryPathAsync(
            extended: false,
            allowDownload: false,
            env.ConfiguredBinaryPath,
            CancellationToken.None);

        await Assert.That(path).IsEqualTo(env.ConfiguredBinaryPath);
        await Assert.That(path).IsNotEqualTo(env.PathBinaryPath);
        await Assert.That(env.Runner.CallCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task GetBinaryPath_ConfiguredPathMissing_ThrowsWithoutFallback(bool allowDownload)
    {
        using var env = new TestEnvironment();
        var provider = env.CreateProvider(withPathBinary: true);
        PlaceCacheBinary(provider);
        var missing = Path.Combine(env.Root, "missing", "pagefind");

        var caught = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetBinaryPathAsync(extended: false, allowDownload, missing, CancellationToken.None));

        await Assert.That(caught!.Message).Contains(missing);
        await Assert.That(caught.Message).Contains("search.binaryPath");
        await Assert.That(env.Http.CallCount).IsEqualTo(0);
        await Assert.That(env.Runner.CallCount).IsEqualTo(0);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public async Task GetBinaryPath_ConfiguredPathNullOrBlank_KeepsExistingResolution(string? configuredPath)
    {
        using var env = new TestEnvironment();
        var provider = env.CreateProvider(withPathBinary: false);
        var cachePath = PlaceCacheBinary(provider);

        var path = await provider.GetBinaryPathAsync(
            extended: false,
            allowDownload: false,
            configuredPath,
            CancellationToken.None);

        await Assert.That(path).IsEqualTo(cachePath);
    }

    private static string PlaceCacheBinary(PagefindBinaryProvider provider)
    {
        var cachePath = provider.GetCacheBinaryPath(extended: false);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        File.WriteAllText(cachePath, "fake cached pagefind");
        return cachePath;
    }

    private sealed class TestEnvironment : IDisposable
    {
        public TestEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), $"kiln-pagefind-configured-{Guid.NewGuid():N}");
            Home = Path.Combine(Root, "home");
            PathDir = Path.Combine(Root, "bin");
            Directory.CreateDirectory(Home);
            Directory.CreateDirectory(PathDir);
            PathBinaryPath = Path.Combine(PathDir, BinaryName);
            ConfiguredBinaryPath = Path.Combine(Root, "tools", "configured-pagefind");
            EnvBinaryPath = Path.Combine(Root, "tools", "env-pagefind");
            Directory.CreateDirectory(Path.GetDirectoryName(ConfiguredBinaryPath)!);
            File.WriteAllText(ConfiguredBinaryPath, "fake");
            File.WriteAllText(EnvBinaryPath, "fake");
        }

        private static string BinaryName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pagefind.exe" : "pagefind";

        public string Root { get; }

        public string Home { get; }

        public string PathDir { get; }

        public string PathBinaryPath { get; }

        public string ConfiguredBinaryPath { get; }

        public string EnvBinaryPath { get; }

        public CountingProcessRunner Runner { get; } = new();

        public CountingHttpHandler Http { get; } = new();

        public PagefindBinaryProvider CreateProvider(bool withPathBinary)
        {
            if (withPathBinary)
                File.WriteAllText(PathBinaryPath, "fake");

            return new PagefindBinaryProvider(Home, Http, PathDir, Runner);
        }

        public void Dispose()
        {
            Http.Dispose();
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }

    private sealed class CountingProcessRunner : IProcessRunner
    {
        public int CallCount { get; private set; }

        public Task<ProcessRunResult> RunAsync(string fileName, string arguments, string? workingDirectory, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new ProcessRunResult(0, "pagefind 1.5.2", string.Empty));
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
