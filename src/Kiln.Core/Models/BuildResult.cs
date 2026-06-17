namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// Result of a site build operation.
/// </summary>
public sealed class BuildResult
{
    public required int TotalFiles { get; init; }
    public required int RenderedFiles { get; init; }
    public required int SkippedDrafts { get; init; }
    public required TimeSpan Duration { get; init; }
    public required string OutputDirectory { get; init; }
    public Collection<string> Warnings { get; init; } = [];
    public Collection<string> Errors { get; init; } = [];
    public bool Success => Errors.Count == 0;
}
