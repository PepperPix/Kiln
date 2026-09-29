namespace Kiln.Services;

public sealed record SearchIndexResult(
    bool Success,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
