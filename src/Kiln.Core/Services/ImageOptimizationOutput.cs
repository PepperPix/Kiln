namespace Kiln.Services;

/// <summary>
/// An optimized image.
/// </summary>
public sealed record ImageOptimizationOutput
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageOptimizationOutput"/> record.
    /// </summary>
    /// <param name="bytes">The encoded image.</param>
    /// <param name="extension">The file extension of the encoded image including the leading dot.</param>
    public ImageOptimizationOutput(byte[] bytes, string extension)
    {
        Bytes = bytes;
        Extension = extension;
    }

    /// <summary>Gets the encoded image.</summary>
    public byte[] Bytes { get; }

    /// <summary>Gets the file extension of the encoded image including the leading dot.</summary>
    public string Extension { get; }
}
