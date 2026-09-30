namespace Kiln.Models;

public sealed class SearchOptions
{
    public bool Enabled { get; init; }
    public bool Extended { get; init; }
    /// <summary>Explicit Pagefind binary; an absolute path once loaded from site configuration (relative values resolve against the project directory).</summary>
    public string? BinaryPath { get; init; }
}
