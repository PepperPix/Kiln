namespace Kiln.Core.Tests.Services;

using Kiln.Services;
using SkiaSharp;

public class SkiaSharpImageOptimizerTests
{
    [Test]
    [Arguments(".png")]
    [Arguments(".jpg")]
    [Arguments(".jpeg")]
    [Arguments(".webp")]
    public async Task CanOptimize_SupportedRasterExtensions_ReturnsTrue(string extension)
    {
        var optimizer = new SkiaSharpImageOptimizer();

        await Assert.That(optimizer.CanOptimize(extension)).IsTrue();
    }

    [Test]
    [Arguments(".gif")]
    [Arguments(".svg")]
    public async Task CanOptimize_UnsupportedExtensions_ReturnsFalse(string extension)
    {
        var optimizer = new SkiaSharpImageOptimizer();

        await Assert.That(optimizer.CanOptimize(extension)).IsFalse();
    }

    [Test]
    public async Task Optimize_LargeImage_DownscalesToMaxWidthPreservingAspectRatio()
    {
        var optimizer = new SkiaSharpImageOptimizer();
        var sourceBytes = CreateTestPng(3000, 2000);
        var settings = new ImageOptimizationSettings(MaxWidth: 1000, Quality: 82, ConvertToWebp: false);

        var result = optimizer.Optimize(sourceBytes, ".png", settings);

        using var decoded = SKBitmap.Decode(result.Bytes);
        await Assert.That(decoded.Width).IsEqualTo(1000);
        await Assert.That(decoded.Height).IsEqualTo(667); // 2000 * (1000/3000), rounded
        await Assert.That(result.Extension).IsEqualTo(".png");
    }

    [Test]
    public async Task Optimize_ImageSmallerThanMaxWidth_DoesNotUpscale()
    {
        var optimizer = new SkiaSharpImageOptimizer();
        var sourceBytes = CreateTestPng(400, 300);
        var settings = new ImageOptimizationSettings(MaxWidth: 2000, Quality: 82, ConvertToWebp: false);

        var result = optimizer.Optimize(sourceBytes, ".png", settings);

        using var decoded = SKBitmap.Decode(result.Bytes);
        await Assert.That(decoded.Width).IsEqualTo(400);
        await Assert.That(decoded.Height).IsEqualTo(300);
    }

    [Test]
    public async Task Optimize_WebpFlag_ChangesExtensionAndProducesWebpMagicBytes()
    {
        var optimizer = new SkiaSharpImageOptimizer();
        var sourceBytes = CreateTestPng(400, 300);
        var settings = new ImageOptimizationSettings(MaxWidth: 2000, Quality: 82, ConvertToWebp: true);

        var result = optimizer.Optimize(sourceBytes, ".png", settings);

        await Assert.That(result.Extension).IsEqualTo(".webp");
        // RIFF....WEBP magic bytes: bytes 0-3 = "RIFF", bytes 8-11 = "WEBP"
        await Assert.That(System.Text.Encoding.ASCII.GetString(result.Bytes, 0, 4)).IsEqualTo("RIFF");
        await Assert.That(System.Text.Encoding.ASCII.GetString(result.Bytes, 8, 4)).IsEqualTo("WEBP");
    }

    [Test]
    public async Task Optimize_NoWebpFlag_KeepsOriginalExtension()
    {
        var optimizer = new SkiaSharpImageOptimizer();
        var sourceBytes = CreateTestJpeg(400, 300);
        var settings = new ImageOptimizationSettings(MaxWidth: 2000, Quality: 82, ConvertToWebp: false);

        var result = optimizer.Optimize(sourceBytes, ".jpg", settings);

        await Assert.That(result.Extension).IsEqualTo(".jpg");
    }

    private static byte[] CreateTestPng(int width, int height) => CreateTestImage(width, height, SKEncodedImageFormat.Png);

    private static byte[] CreateTestJpeg(int width, int height) => CreateTestImage(width, height, SKEncodedImageFormat.Jpeg);

    private static byte[] CreateTestImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(120, 140, 160));
        using var data = bitmap.Encode(format, 90);
        return data.ToArray();
    }
}
