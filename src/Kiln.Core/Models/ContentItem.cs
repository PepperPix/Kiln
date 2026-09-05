namespace Kiln.Models;

public sealed class ContentItem
{
    public string? Id { get; init; }
    public required string Title { get; init; }
    public DateTime? Date { get; init; }
    public bool Draft { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// Plain-text teaser/excerpt for listings, derived from a fallback chain:
    /// <see cref="Description"/> if set, otherwise content before a <c>&lt;!--more--&gt;</c>
    /// marker, otherwise an automatic word-count truncation of the body.
    /// </summary>
    public string? Teaser { get; init; }

    public string? Layout { get; init; }
    public int Weight { get; init; }
    public required string SourcePath { get; init; }
    public required string RelativePath { get; init; }
    public required string RawContent { get; init; }
    public required string HtmlContent { get; init; }
    public required Uri Url { get; set; }
    public required string OutputPath { get; set; }
    public required ContentGroup Collection { get; init; }
    public Dictionary<string, object> Extra { get; init; } = [];
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
    /// tag is injected into the rendered output regardless of theme (see ADR-066 amendment) — themes
    /// do not need to (and should not) render this tag themselves.
    /// </summary>
    public bool NoIndex { get; init; }

    public ContentItem? Next { get; set; }
    public ContentItem? Prev { get; set; }
    public Dictionary<string, ContentItem> ResolvedReferences { get; } = [];
}

