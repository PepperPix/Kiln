namespace Kiln.Models;

/// <summary>
/// One page of a paginated list of items.
/// </summary>
public sealed class Paginator
{
    /// <summary>Gets the items on this page.</summary>
    public required IReadOnlyList<ContentItem> Items { get; init; }

    /// <summary>Gets the number of this page, starting at 1.</summary>
    public required int Page { get; init; }

    /// <summary>Gets the total number of pages.</summary>
    public required int TotalPages { get; init; }

    /// <summary>Gets the total number of items across all pages.</summary>
    public required int TotalItems { get; init; }

    /// <summary>Gets the URL of the next page, or <see langword="null"/> on the last page.</summary>
    public Uri? NextUrl { get; init; }

    /// <summary>Gets the URL of the previous page, or <see langword="null"/> on the first page.</summary>
    public Uri? PrevUrl { get; init; }
}
