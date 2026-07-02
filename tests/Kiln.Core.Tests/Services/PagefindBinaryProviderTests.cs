namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PagefindBinaryProviderTests
{
    [Test]
    public async Task GetBinaryPath_EnvOverride_ReturnsOverridePath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(tempFile, "fake");
        try
        {
            Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", tempFile);
            var provider = new PagefindBinaryProvider(Path.GetTempPath());
            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(tempFile);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", null);
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public async Task GetBinaryPath_EnvOverride_NonExistentFile_ContinuesToNextStep()
    {
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", "/nonexistent/path/pagefind");
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        try
        {
            // Place a fake binary in the cache so it resolves there (not via download)
            var provider = new PagefindBinaryProvider(fakeHome);
            var cachePath = provider.GetCacheBinaryPath(extended: false);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, "fake");

            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(cachePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", null);
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_CacheHit_ReturnsCachePath()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        var savedOverride = Environment.GetEnvironmentVariable("KILN_PAGEFIND_PATH");
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", null);
        try
        {
            var provider = new PagefindBinaryProvider(fakeHome);
            var cachePath = provider.GetCacheBinaryPath(extended: false);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, "fake pagefind binary");

            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(cachePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", savedOverride);
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_ExtendedCacheHit_ReturnsCachePath()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        var savedOverride = Environment.GetEnvironmentVariable("KILN_PAGEFIND_PATH");
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", null);
        try
        {
            var provider = new PagefindBinaryProvider(fakeHome);
            var cachePath = provider.GetCacheBinaryPath(extended: true);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, "fake pagefind_extended binary");

            var path = await provider.GetBinaryPathAsync(extended: true, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(cachePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", savedOverride);
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_NothingFound_NoDownload_ThrowsWithHint()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        var savedOverride = Environment.GetEnvironmentVariable("KILN_PAGEFIND_PATH");
        Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", null);
        try
        {
            var provider = new PagefindBinaryProvider(fakeHome);

            InvalidOperationException? caughtEx = null;
            try
            {
                await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                caughtEx = ex;
            }

            await Assert.That(caughtEx).IsNotNull();
            await Assert.That(caughtEx!.Message).Contains("KILN_PAGEFIND_PATH");
            await Assert.That(caughtEx.Message).Contains("npx pagefind");
        }
        finally
        {
            Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", savedOverride);
        }
    }

    [Test]
    public async Task GetCacheBinaryPath_ContainsVersionAndBinaryName()
    {
        const string fakeHome = "/fake/home";
        var provider = new PagefindBinaryProvider(fakeHome);
        var cachePath = provider.GetCacheBinaryPath(extended: false);

        await Assert.That(cachePath).Contains(PagefindBinaryProvider.Version);
        await Assert.That(cachePath).Contains(".kiln");
        await Assert.That(cachePath).Contains("pagefind");
    }
}

