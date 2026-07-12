namespace Kiln.Core.Tests.Services;

using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Kiln.Services;

public class PagefindBinaryProviderTests
{
    private string? _savedPagefindPathOverride;

    /// <summary>
    /// Guarantees a clean, deterministic starting state for every test: whatever
    /// <c>KILN_PAGEFIND_PATH</c> is actually set to on the developer/CI machine must never leak
    /// into a test that doesn't expect it. Tests that specifically exercise the override (e.g.
    /// <see cref="GetBinaryPath_EnvOverride_ReturnsOverridePath"/>) still set their own value
    /// inside the test body.
    /// </summary>
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
    public async Task GetBinaryPath_EnvOverride_ReturnsOverridePath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await File.WriteAllTextAsync(tempFile, "fake");
        try
        {
            Environment.SetEnvironmentVariable("KILN_PAGEFIND_PATH", tempFile);
            var provider = new PagefindBinaryProvider(Path.GetTempPath(), httpMessageHandler: null, pathOverride: string.Empty);
            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(tempFile);
        }
        finally
        {
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
            var provider = new PagefindBinaryProvider(fakeHome, httpMessageHandler: null, pathOverride: string.Empty);
            var cachePath = provider.GetCacheBinaryPath(extended: false);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, "fake");

            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(cachePath);
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_CacheHit_ReturnsCachePath()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        try
        {
            var provider = new PagefindBinaryProvider(fakeHome, httpMessageHandler: null, pathOverride: string.Empty);
            var cachePath = provider.GetCacheBinaryPath(extended: false);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, "fake pagefind binary");

            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(cachePath);
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_ExtendedCacheHit_ReturnsCachePath()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        try
        {
            var provider = new PagefindBinaryProvider(fakeHome, httpMessageHandler: null, pathOverride: string.Empty);
            var cachePath = provider.GetCacheBinaryPath(extended: true);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, "fake pagefind_extended binary");

            var path = await provider.GetBinaryPathAsync(extended: true, allowDownload: false, CancellationToken.None);
            await Assert.That(path).IsEqualTo(cachePath);
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_NothingFound_NoDownload_ThrowsWithHint()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        var provider = new PagefindBinaryProvider(fakeHome, httpMessageHandler: null, pathOverride: string.Empty);

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

    [Test]
    public async Task GetCacheBinaryPath_ContainsVersionAndBinaryName()
    {
        const string fakeHome = "/fake/home";
        var provider = new PagefindBinaryProvider(fakeHome, httpMessageHandler: null, pathOverride: string.Empty);
        var cachePath = provider.GetCacheBinaryPath(extended: false);

        await Assert.That(cachePath).Contains(PagefindBinaryProvider.Version);
        await Assert.That(cachePath).Contains(".kiln");
        await Assert.That(cachePath).Contains("pagefind");
    }

    [Test]
    public async Task GetBinaryPath_Download_Success_ExtractsVerifiedBinary()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        try
        {
            var binaryFileName = ExpectedBinaryFileName(extended: false);
            var binaryContent = "#!/bin/sh\necho fake-pagefind\n"u8.ToArray();
            var tarGzBytes = BuildTarGz(binaryFileName, binaryContent);
            var expectedHash = Convert.ToHexString(SHA256.HashData(tarGzBytes));

            using var handler = new FakeHttpMessageHandler($"{expectedHash} *pagefind.tar.gz", tarGzBytes);
            var provider = new PagefindBinaryProvider(fakeHome, handler, pathOverride: string.Empty);

            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: true, CancellationToken.None);

            await Assert.That(File.Exists(path)).IsTrue();
            var extractedContent = await File.ReadAllBytesAsync(path);
            await Assert.That(extractedContent.SequenceEqual(binaryContent)).IsTrue();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mode = File.GetUnixFileMode(path);
                await Assert.That(mode.HasFlag(UnixFileMode.UserExecute)).IsTrue();
            }
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_Download_Sha256Mismatch_ThrowsAndLeavesNoCacheFile()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        try
        {
            var binaryFileName = ExpectedBinaryFileName(extended: false);
            var tarGzBytes = BuildTarGz(binaryFileName, "fake-pagefind"u8.ToArray());
            var wrongHash = new string('0', 64);

            using var handler = new FakeHttpMessageHandler(wrongHash, tarGzBytes);
            var provider = new PagefindBinaryProvider(fakeHome, handler, pathOverride: string.Empty);

            InvalidOperationException? caughtEx = null;
            try
            {
                await provider.GetBinaryPathAsync(extended: false, allowDownload: true, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                caughtEx = ex;
            }

            await Assert.That(caughtEx).IsNotNull();
            await Assert.That(caughtEx!.Message).Contains("SHA256 mismatch");

            var cachePath = provider.GetCacheBinaryPath(extended: false);
            await Assert.That(File.Exists(cachePath)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_Download_BinaryMissingFromArchive_Throws()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        try
        {
            var tarGzBytes = BuildTarGz("not-the-expected-binary", "irrelevant"u8.ToArray());
            var expectedHash = Convert.ToHexString(SHA256.HashData(tarGzBytes));

            using var handler = new FakeHttpMessageHandler(expectedHash, tarGzBytes);
            var provider = new PagefindBinaryProvider(fakeHome, handler, pathOverride: string.Empty);

            InvalidOperationException? caughtEx = null;
            try
            {
                await provider.GetBinaryPathAsync(extended: false, allowDownload: true, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                caughtEx = ex;
            }

            await Assert.That(caughtEx).IsNotNull();
            await Assert.That(caughtEx!.Message).Contains("not found in archive");
        }
        finally
        {
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    [Test]
    public async Task GetBinaryPath_PathOverrideInjected_ReturnsBinaryFoundInOverriddenPathDirectory()
    {
        var fakeHome = Path.Combine(Path.GetTempPath(), $"kiln-test-home-{Guid.NewGuid():N}");
        var fakePathDir = Path.Combine(Path.GetTempPath(), $"kiln-test-pathdir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fakePathDir);
        try
        {
            var binaryFileName = ExpectedBinaryFileName(extended: false);
            var fakeBinaryPath = Path.Combine(fakePathDir, binaryFileName);
            await File.WriteAllTextAsync(fakeBinaryPath, "fake pagefind on injected PATH");

            var provider = new PagefindBinaryProvider(fakeHome, httpMessageHandler: null, pathOverride: fakePathDir);

            var path = await provider.GetBinaryPathAsync(extended: false, allowDownload: false, CancellationToken.None);

            await Assert.That(path).IsEqualTo(fakeBinaryPath);
        }
        finally
        {
            if (Directory.Exists(fakePathDir))
                Directory.Delete(fakePathDir, true);
            if (Directory.Exists(fakeHome))
                Directory.Delete(fakeHome, true);
        }
    }

    private static string ExpectedBinaryFileName(bool extended)
    {
        var baseName = extended ? "pagefind_extended" : "pagefind";
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{baseName}.exe" : baseName;
    }

    private static byte[] BuildTarGz(string entryName, byte[] content)
    {
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            using var tarWriter = new TarWriter(gzip, leaveOpen: true);
            var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
            {
                DataStream = new MemoryStream(content),
            };
            tarWriter.WriteEntry(entry);
        }

        return ms.ToArray();
    }

    private sealed class FakeHttpMessageHandler(string sha256ResponseBody, byte[] tarballBytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isChecksumRequest = request.RequestUri!.AbsoluteUri.EndsWith(".sha256", StringComparison.Ordinal);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = isChecksumRequest
                    ? new StringContent(sha256ResponseBody)
                    : new ByteArrayContent(tarballBytes),
            };
            return Task.FromResult(response);
        }
    }
}


