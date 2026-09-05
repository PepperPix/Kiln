namespace Kiln.Services;

public sealed record ImageOptimizationOutput
{
    public ImageOptimizationOutput(byte[] bytes, string extension)
    {
        Bytes = bytes;
        Extension = extension;
    }

    public byte[] Bytes { get; }

    public string Extension { get; }
}
