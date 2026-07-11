namespace Kiln.Services;

public interface IImageOptimizer
{
    bool CanOptimize(string extension);

    ImageOptimizationOutput Optimize(byte[] sourceBytes, string sourceExtension, ImageOptimizationSettings settings);
}

public sealed record ImageOptimizationSettings(int MaxWidth, int Quality, bool ConvertToWebp);

public sealed record ImageOptimizationOutput
{
    public ImageOptimizationOutput(byte[] bytes, string extension)
    {
        Bytes = bytes;
        Extension = extension;
    }

#pragma warning disable CA1819 // Intentional: this carries raw encoded image bytes to be written to disk, not a settable collection API
    public byte[] Bytes { get; }
#pragma warning restore CA1819

    public string Extension { get; }
}
