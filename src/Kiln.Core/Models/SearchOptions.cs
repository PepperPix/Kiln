namespace Kiln.Models;

public sealed class SearchOptions
{
    public bool Enabled { get; init; }
    public bool Extended { get; init; }
    public string? BinaryPath { get; init; }
}
