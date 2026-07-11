namespace Kiln.Models;

public sealed class ImageOptions
{
    public const int DefaultMaxWidth = 2000;
    public const int DefaultQuality = 82;

    public bool Enabled { get; init; } = true;
    public int MaxWidth { get; init; } = DefaultMaxWidth;
    public int Quality { get; init; } = DefaultQuality;
    public bool Webp { get; init; }
    public IReadOnlyList<string> Exclude { get; init; } = [];
}
