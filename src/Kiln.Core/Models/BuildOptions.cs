namespace Kiln.Models;

/// <summary>
/// Settings for the production asset pipeline, read from the <c>build</c> section of <c>site.yaml</c>.
/// </summary>
public sealed class BuildOptions
{
    /// <summary>Gets whether CSS files below the asset prefix are minified. The default is <see langword="true"/>.</summary>
    public bool MinifyCss { get; init; } = true;

    /// <summary>Gets whether JavaScript files below the asset prefix are minified. The default is <see langword="true"/>.</summary>
    public bool MinifyJs { get; init; } = true;

    /// <summary>Gets whether generated HTML files are minified. The default is <see langword="true"/>.</summary>
    public bool MinifyHtml { get; init; } = true;

    /// <summary>Gets whether SVG files below the asset prefix are minified. The default is <see langword="true"/>.</summary>
    public bool MinifySvg { get; init; } = true;

    /// <summary>
    /// Gets whether HTML minification also removes optional tags. This applies only when the built-in
    /// Nuglify minifier is used. The default is <see langword="false"/>.
    /// </summary>
    public bool HtmlAggressive { get; init; }

    /// <summary>
    /// Gets whether asset file names receive a content hash and references in the HTML are rewritten accordingly.
    /// The default is <see langword="true"/>.
    /// </summary>
    public bool Fingerprint { get; init; } = true;

    /// <summary>
    /// Gets whether internal references in the generated HTML and CSS are checked for missing targets; each dead
    /// link is reported as a build error. The default is <see langword="true"/>.
    /// </summary>
    public bool LinkCheck { get; init; } = true;
}
