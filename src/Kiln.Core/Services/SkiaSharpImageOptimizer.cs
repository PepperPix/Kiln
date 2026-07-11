namespace Kiln.Services;

using SkiaSharp;

/// <summary>
/// Optimizes raster images using SkiaSharp: proportional downscale to a maximum width,
/// recompression at a target quality, and optional conversion to WebP. Vector (SVG) and
/// animated (GIF) formats are intentionally excluded — see <see cref="CanOptimize"/>.
/// </summary>
public sealed class SkiaSharpImageOptimizer : IImageOptimizer
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp",
    };

    public bool CanOptimize(string extension) => SupportedExtensions.Contains(extension);

    public ImageOptimizationOutput Optimize(byte[] sourceBytes, string sourceExtension, ImageOptimizationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(sourceBytes);
        ArgumentNullException.ThrowIfNull(sourceExtension);
        ArgumentNullException.ThrowIfNull(settings);

        using var source = SKBitmap.Decode(sourceBytes)
            ?? throw new InvalidOperationException("Could not decode image data.");

        var resized = source.Width > settings.MaxWidth ? Resize(source, settings.MaxWidth) : null;
        try
        {
            var bitmapToEncode = resized ?? source;
            var format = settings.ConvertToWebp ? SKEncodedImageFormat.Webp : FormatFromExtension(sourceExtension);
            var resultExtension = settings.ConvertToWebp ? ".webp" : sourceExtension;

            using var data = bitmapToEncode.Encode(format, settings.Quality)
                ?? throw new InvalidOperationException("Failed to encode optimized image.");

            return new ImageOptimizationOutput(data.ToArray(), resultExtension);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static SKBitmap Resize(SKBitmap source, int maxWidth)
    {
        var newWidth = maxWidth;
        var newHeight = (int)Math.Round(source.Height * (maxWidth / (double)source.Width));
        var info = new SKImageInfo(newWidth, newHeight, source.ColorType, source.AlphaType, source.ColorSpace);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        return source.Resize(info, sampling)
            ?? throw new InvalidOperationException("Failed to resize image.");
    }

    private static SKEncodedImageFormat FormatFromExtension(string extension)
    {
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            return SKEncodedImageFormat.Png;
        if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
            return SKEncodedImageFormat.Jpeg;
        if (string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
            return SKEncodedImageFormat.Webp;
        throw new NotSupportedException($"Unsupported image extension: {extension}");
    }
}
