namespace Kiln.Services;

/// <summary>
/// Optimizes raster images.
/// </summary>
public interface IImageOptimizer
{
    /// <summary>
    /// Determines whether images with the given file extension can be optimized.
    /// </summary>
    /// <param name="extension">The file extension including the leading dot, for example <c>.png</c>.</param>
    /// <returns><see langword="true"/> if the extension is supported; otherwise <see langword="false"/>.</returns>
    bool CanOptimize(string extension);

    /// <summary>
    /// Optimizes an image.
    /// </summary>
    /// <param name="sourceBytes">The encoded source image.</param>
    /// <param name="sourceExtension">The file extension of the source image including the leading dot.</param>
    /// <param name="settings">The optimization settings.</param>
    /// <returns>The optimized image and its file extension.</returns>
    ImageOptimizationOutput Optimize(byte[] sourceBytes, string sourceExtension, ImageOptimizationSettings settings);
}
