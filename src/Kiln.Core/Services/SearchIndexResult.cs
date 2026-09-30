namespace Kiln.Services;

/// <summary>
/// The outcome of building the search index with <see cref="ISearchIndexer"/>.
/// </summary>
/// <param name="Success">Whether the index was built.</param>
/// <param name="Warnings">Warnings from the indexing run.</param>
/// <param name="Errors">Error messages when the run failed.</param>
public sealed record SearchIndexResult(
    bool Success,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
