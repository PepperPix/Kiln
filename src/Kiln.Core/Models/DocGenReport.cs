namespace Kiln.Models;

public sealed record DocGenReport(
    IReadOnlyList<string> Written,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Warnings);
