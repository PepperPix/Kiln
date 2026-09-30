namespace Kiln.Models;

using YamlDotNet.Serialization;

/// <summary>
/// The front matter block at the top of a content file, deserialized from YAML.
/// </summary>
public sealed class FrontMatter
{
    /// <summary>Gets an optional identifier of the item.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the title of the item.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the date of the item.</summary>
    public DateTime? Date { get; init; }

    /// <summary>Gets whether the item is a draft.</summary>
    public bool Draft { get; init; }

    /// <summary>Gets the name of the layout used to render the item.</summary>
    public string? Layout { get; init; }

    /// <summary>Gets the slug of the item.</summary>
    public string? Slug { get; init; }

    /// <summary>Gets the description of the item.</summary>
    public string? Description { get; init; }

    /// <summary>Gets a URL that overrides the permalink pattern of the collection. It is read from the <c>url</c> key.</summary>
    [YamlMember(Alias = "url")]
    public string? PermalinkOverride { get; init; }

    /// <summary>Gets the weight used for ordering.</summary>
    public int Weight { get; init; }

    /// <summary>Gets whether the item is excluded from the sitemap and marked as not to be indexed by search engines.</summary>
    public bool NoIndex { get; init; }

    /// <summary>Gets whether images of the item take part in image optimization. When unset, optimization is enabled.</summary>
    public bool? ImageOptimization { get; init; }

    /// <summary>Gets the values of the <c>extra</c> block.</summary>
    public Dictionary<string, object> Extra { get; init; } = [];
}
