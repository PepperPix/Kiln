namespace Kiln.Core.Tests.Services;

using System.Runtime.InteropServices;
using Kiln.Services;

/// <summary>
/// Reads and writes the process-wide <c>KILN_PAGEFIND_PATH</c> variable, so it must not run in parallel
/// with the other Pagefind provider tests (see <see cref="PagefindBinaryProviderTests"/>).
/// </summary>
[NotInParallel]
public class PagefindBinaryProviderVersionTests
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
    [Arguments("pagefind 1.4.0")]
    [Arguments("pagefind 1.4.9")]
    [Arguments("pagefind 0.12.0")]
    [Arguments("pagefind_extended 1.3")]
    public async Task GetBinaryPath_PathBinaryOlderThanMinimum_IsSkippedAndCacheUsed(string versionOutput)
    {
        using var env = new TestEnvironment();
        var runner = new VersionRunner(_ => new ProcessRunResult(0, versionOutput, string.Empty));
        var provider = env.CreateProvider(runner);
        var cachePath = TestEnvironment.PlaceCacheBinary(provider);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(cachePath);
        await Assert.That(runner.Calls.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("pagefind 1.5.0")]
    [Arguments("pagefind 1.5.2")]
    [Arguments("pagefind_extended 1.10.0")]
    [Arguments("pagefind 2.0.1\n")]
    public async Task GetBinaryPath_PathBinaryMeetsMinimum_IsUsed(string versionOutput)
    {
        using var env = new TestEnvironment();
        var runner = new VersionRunner(_ => new ProcessRunResult(0, versionOutput, string.Empty));
        var provider = env.CreateProvider(runner);
        TestEnvironment.PlaceCacheBinary(provider);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(env.PathBinaryPath);
        await Assert.That(runner.Calls[0].Arguments).IsEqualTo("--version");
    }

    [Test]
    public async Task GetBinaryPath_VersionOnStdErr_IsParsed()
    {
        using var env = new TestEnvironment();
        var runner = new VersionRunner(_ => new ProcessRunResult(0, string.Empty, "pagefind 1.5.2"));
        var provider = env.CreateProvider(runner);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(env.PathBinaryPath);
    }

    [Test]
    public async Task GetBinaryPath_UnreadableVersionOutput_IsSkipped()
    {
        using var env = new TestEnvironment();
        var runner = new VersionRunner(_ => new ProcessRunResult(0, "no version here", string.Empty));
        var provider = env.CreateProvider(runner);
        var cachePath = TestEnvironment.PlaceCacheBinary(provider);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(cachePath);
    }

    [Test]
    public async Task GetBinaryPath_VersionCommandFails_IsSkipped()
    {
        using var env = new TestEnvironment();
        var runner = new VersionRunner(_ => new ProcessRunResult(1, "pagefind 1.5.2", "boom"));
        var provider = env.CreateProvider(runner);
        var cachePath = TestEnvironment.PlaceCacheBinary(provider);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(cachePath);
    }

    [Test]
    public async Task GetBinaryPath_VersionCommandThrows_IsSkipped()
    {
        using var env = new TestEnvironment();
        var runner = new VersionRunner(_ => throw new InvalidOperationException("cannot start"));
        var provider = env.CreateProvider(runner);
        var cachePath = TestEnvironment.PlaceCacheBinary(provider);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(cachePath);
    }

    [Test]
    public async Task GetBinaryPath_OldPathBinaryAndNoDownload_ErrorNamesPathAndVersion()
    {
        using var env = new TestEnvironment();
        var runner = new VersionRunner(_ => new ProcessRunResult(0, "pagefind 1.4.0", string.Empty));
        var provider = env.CreateProvider(runner);

        await Assert.That(async () => await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining(env.PathBinaryPath)
            .And.WithMessageContaining("1.4.0")
            .And.WithMessageContaining("1.5.0");
    }

    [Test]
    public async Task GetBinaryPath_EnvOverride_IsNeverVersionChecked()
    {
        using var env = new TestEnvironment();
        var overrideFile = Path.Combine(env.Root, "custom-pagefind");
        await File.WriteAllTextAsync(overrideFile, "fake");
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", overrideFile);
        var runner = new VersionRunner(_ => new ProcessRunResult(0, "pagefind 0.1.0", string.Empty));
        var provider = env.CreateProvider(runner);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(overrideFile);
        await Assert.That(runner.Calls.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetBinaryPath_WithoutProcessRunner_UsesPathBinaryUnchecked()
    {
        using var env = new TestEnvironment();
        var provider = new PagefindBinaryProvider(env.Home, httpMessageHandler: null, pathOverride: env.PathDir);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(env.PathBinaryPath);
    }

    [Test]
    public async Task GetBinaryPath_OldBinaryEarlierOnPath_NewerLaterOnPathWins()
    {
        using var env = new TestEnvironment();
        var newerDir = Path.Combine(env.Root, "bin-new");
        Directory.CreateDirectory(newerDir);
        var newerBinary = Path.Combine(newerDir, TestEnvironment.BinaryName);
        await File.WriteAllTextAsync(newerBinary, "fake");
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var runner = new VersionRunner(request => new ProcessRunResult(
            0,
            request == env.PathBinaryPath ? "pagefind 1.4.0" : "pagefind 1.5.2",
            string.Empty));
        var provider = new PagefindBinaryProvider(env.Home, null, $"{env.PathDir}{separator}{newerDir}", runner);

        var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

        await Assert.That(path).IsEqualTo(newerBinary);
    }

    private sealed class TestEnvironment : IDisposable
    {
        public TestEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), $"kiln-pagefind-version-{Guid.NewGuid():N}");
            Home = Path.Combine(Root, "home");
            PathDir = Path.Combine(Root, "bin");
            Directory.CreateDirectory(Home);
            Directory.CreateDirectory(PathDir);
            PathBinaryPath = Path.Combine(PathDir, BinaryName);
            File.WriteAllText(PathBinaryPath, "fake");
        }

        public static string BinaryName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pagefind.exe" : "pagefind";

        public string Root { get; }

        public string Home { get; }

        public string PathDir { get; }

        public string PathBinaryPath { get; }

        public PagefindBinaryProvider CreateProvider(IProcessRunner runner)
            => new(Home, httpMessageHandler: null, pathOverride: PathDir, runner);

        public static string PlaceCacheBinary(PagefindBinaryProvider provider)
        {
            var cachePath = provider.GetCacheBinaryPath(extended: false);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(cachePath, "fake cached pagefind");
            return cachePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }

    private sealed class VersionRunner(Func<string, ProcessRunResult> respond) : IProcessRunner
    {
        public List<(string FileName, string Arguments)> Calls { get; } = [];

        public Task<ProcessRunResult> RunAsync(string fileName, string arguments, string? workingDirectory, CancellationToken ct)
        {
            Calls.Add((fileName, arguments));
            return Task.FromResult(respond(fileName));
        }
    }
}
