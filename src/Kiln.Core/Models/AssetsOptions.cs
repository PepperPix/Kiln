namespace Kiln.Models;

/// <summary>
/// Asset settings of a site, read from the <c>assets</c> section of <c>site.yaml</c>.
/// </summary>
public sealed class AssetsOptions
{
    /// <summary>
    /// Gets the identifier of the <see cref="Kiln.Abstractions.IAssetMinifier"/> used for production builds. The default is <c>nuglify</c>.
    /// </summary>
    public string Minifier { get; init; } = "nuglify";
}
