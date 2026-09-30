namespace Kiln.Services;

/// <summary>
/// The settings for optimizing one image with <see cref="IImageOptimizer"/>.
/// </summary>
/// <param name="MaxWidth">The maximum width in pixels.</param>
/// <param name="Quality">The encoder quality.</param>
/// <param name="ConvertToWebp">Whether the image is converted to WebP.</param>
public sealed record ImageOptimizationSettings(int MaxWidth, int Quality, bool ConvertToWebp);
