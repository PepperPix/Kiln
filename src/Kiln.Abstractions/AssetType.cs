namespace Kiln.Abstractions;

/// <summary>
/// Identifies the kind of an <see cref="Asset"/>; minifiers use it to select their handling.
/// </summary>
public enum AssetType
{
    /// <summary>A CSS stylesheet.</summary>
    Css,

    /// <summary>A JavaScript file.</summary>
    Js,

    /// <summary>An HTML document.</summary>
    Html,

    /// <summary>An SVG image.</summary>
    Svg,

    /// <summary>Any other kind of file.</summary>
    Other,
}
