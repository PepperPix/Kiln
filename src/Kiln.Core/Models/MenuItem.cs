namespace Kiln.Models;

using System.Collections.ObjectModel;

public sealed class MenuItem
{
    public required string Title { get; init; }
    public Uri? Url { get; set; }          // Resolved during build
    public string? Ref { get; init; }          // Raw reference, e.g. "posts/" or "pages/about"
    public bool External { get; init; }
    public bool Active { get; set; }           // Computed per render call
    public Collection<MenuItem> Children { get; init; } = [];
}
