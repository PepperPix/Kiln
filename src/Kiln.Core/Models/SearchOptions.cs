namespace Kiln.Models;

/// <summary>
/// Search index settings, read from the <c>search</c> section of <c>site.yaml</c>.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>Gets whether a search index is built for the site.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets whether the extended edition of Pagefind is used.</summary>
    public bool Extended { get; init; }

    /// <summary>Explicit Pagefind binary; an absolute path once loaded from site configuration (relative values resolve against the project directory).</summary>
    public string? BinaryPath { get; init; }
}
