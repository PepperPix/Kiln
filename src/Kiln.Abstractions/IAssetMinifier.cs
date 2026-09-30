namespace Kiln.Abstractions;

/// <summary>
/// Minifies text-based assets during a production build.
/// </summary>
public interface IAssetMinifier
{
    /// <summary>
    /// Gets the identifier used to select this minifier through the <c>assets.minifier</c> site setting.
    /// The comparison is case-insensitive.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Determines whether this minifier can handle the given asset type.
    /// </summary>
    /// <param name="type">The asset type.</param>
    /// <returns><see langword="true"/> if <see cref="Minify"/> can process assets of this type; otherwise <see langword="false"/>.</returns>
    bool CanMinify(AssetType type);

    /// <summary>
    /// Minifies the given content.
    /// </summary>
    /// <param name="content">The asset content as text.</param>
    /// <param name="type">The asset type of the content.</param>
    /// <returns>The minified content.</returns>
    string Minify(string content, AssetType type);
}
