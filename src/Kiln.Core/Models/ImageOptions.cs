namespace Kiln.Models;

/// <summary>
/// Image optimization settings, read from the <c>images</c> section of <c>site.yaml</c>. Optimization runs in production builds.
/// </summary>
public sealed class ImageOptions
{
    /// <summary>The default maximum image width in pixels.</summary>
    public const int DefaultMaxWidth = 2000;

    /// <summary>The default encoder quality.</summary>
    public const int DefaultQuality = 82;

    /// <summary>Gets whether image optimization is enabled. The default is <see langword="true"/>.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the maximum width in pixels; wider images are scaled down proportionally.</summary>
    public int MaxWidth { get; init; } = DefaultMaxWidth;

    /// <summary>Gets the encoder quality used when images are re-encoded.</summary>
    public int Quality { get; init; } = DefaultQuality;

    /// <summary>Gets whether optimized images are converted to WebP.</summary>
    public bool Webp { get; init; }

    /// <summary>Gets glob patterns, matched case-insensitively against the project-relative path of a source image, for images that are not optimized.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];
}
