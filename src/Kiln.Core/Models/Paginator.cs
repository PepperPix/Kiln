namespace Kiln.Models;

public sealed class Paginator
{
    public required IReadOnlyList<ContentItem> Items { get; init; }
    public required int Page { get; init; }
    public required int TotalPages { get; init; }
    public required int TotalItems { get; init; }
    public Uri? NextUrl { get; init; }
    public Uri? PrevUrl { get; init; }
}
