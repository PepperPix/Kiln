namespace Kiln.Models;

using System.Collections.ObjectModel;

/// <summary>
/// Result of a site build operation.
/// </summary>
public sealed class BuildResult
{
    /// <summary>Gets the number of content items that were read.</summary>
    public required int TotalFiles { get; init; }

    /// <summary>Gets the number of pages that were rendered.</summary>
    public required int RenderedFiles { get; init; }

    /// <summary>Gets the number of draft items that were skipped.</summary>
    public required int SkippedDrafts { get; init; }

    /// <summary>Gets the elapsed build time.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Gets the path of the output directory.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Gets the warnings collected during the build.</summary>
    public Collection<string> Warnings { get; init; } = [];

    /// <summary>Gets the errors collected during the build.</summary>
    public Collection<string> Errors { get; init; } = [];

    /// <summary>Gets a value indicating whether the build finished without errors.</summary>
    public bool Success => Errors.Count == 0;
}
