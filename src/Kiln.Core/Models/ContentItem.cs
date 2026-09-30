namespace Kiln.Models;

/// <summary>
/// A single piece of content, read from a Markdown file with front matter.
/// </summary>
public sealed class ContentItem
{
    /// <summary>Gets the identifier from the front matter, if any.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the title of the item.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the date of the item.</summary>
    public DateTime? Date { get; init; }

    /// <summary>Gets whether the item is a draft. Drafts are skipped unless a build includes them.</summary>
    public bool Draft { get; init; }

    /// <summary>
    /// Gets the slug of the item. It is taken from the front matter; otherwise it is the directory name for a
    /// page bundle or the file name without extension.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>Gets the description from the front matter, if any.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Plain-text teaser/excerpt for listings, derived from a fallback chain:
    /// <see cref="Description"/> if set, otherwise content before a <c>&lt;!--more--&gt;</c>
    /// marker, otherwise an automatic word-count truncation of the body.
    /// </summary>
    public string? Teaser { get; init; }

    /// <summary>Gets the name of the layout from the front matter. When unset, the layout of the collection is used.</summary>
    public string? Layout { get; init; }

    /// <summary>Gets the weight from the front matter, used for ordering.</summary>
    public int Weight { get; init; }

    /// <summary>Gets the path of the source Markdown file.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Gets the path of the source file relative to the collection directory, using '/' separators.</summary>
    public required string RelativePath { get; init; }

    /// <summary>Gets the Markdown body of the item after shortcode processing.</summary>
    public required string RawContent { get; init; }

    /// <summary>Gets the body of the item rendered to HTML.</summary>
    public required string HtmlContent { get; init; }

    /// <summary>Gets or sets the URL of the item. The builder assigns the final URL from the collection's permalink pattern.</summary>
    public required Uri Url { get; set; }

    /// <summary>Gets or sets the path of the generated file relative to the output directory. The builder assigns it from <see cref="Url"/>.</summary>
    public required string OutputPath { get; set; }

    /// <summary>Gets the collection the item belongs to.</summary>
    public required ContentGroup Collection { get; init; }

    /// <summary>Gets the custom front matter values: the <c>extra</c> block plus all keys that are neither known front matter fields nor taxonomy names.</summary>
    public Dictionary<string, object> Extra { get; init; } = [];

    /// <summary>Gets the taxonomy terms of the item, keyed by taxonomy name. Each value is a list of term names.</summary>
    public Dictionary<string, object> Taxonomies { get; init; } = [];

    /// <summary>
    /// Relative path of the section (directory) this item resides in, relative to
    /// the collection root, using '/' separators. Empty string for flat/root items.
    /// </summary>
    public string SectionPath { get; init; } = "";

    /// <summary>
    /// Path to the directory containing co-located assets (Page Bundle).
    /// Null when the item is a plain .md file.
    /// </summary>
    public string? AssetDirectory { get; init; }

    /// <summary>
    /// Opt-out for the production-only image optimization pipeline. Set from the
    /// <c>image_optimization</c> front matter key; defaults to <c>true</c> (optimize).
    /// </summary>
    public bool ImageOptimization { get; init; } = true;

    /// <summary>
    /// Excludes this item from sitemap.xml when <c>true</c>. Set from the <c>no_index</c> front
    /// matter key. The engine guarantees a <c>&lt;meta name="robots" content="noindex, nofollow"&gt;</c>
    /// tag is injected into the rendered output regardless of theme, so themes do not need to (and should
    /// not) render this tag themselves.
    /// </summary>
    public bool NoIndex { get; init; }

    /// <summary>Gets or sets the next published item in the collection. The builder assigns it during a build.</summary>
    public ContentItem? Next { get; set; }

    /// <summary>Gets or sets the previous published item in the collection. The builder assigns it during a build.</summary>
    public ContentItem? Prev { get; set; }

    /// <summary>Gets the items referenced by this item, keyed by the front matter key. The builder resolves them from the collection's <see cref="ContentGroup.References"/>.</summary>
    public Dictionary<string, ContentItem> ResolvedReferences { get; } = [];
}

