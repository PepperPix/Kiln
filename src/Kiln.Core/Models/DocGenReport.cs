namespace Kiln.Models;

/// <summary>
/// The outcome of generating reference pages with <see cref="Services.IXmlDocGenerator"/> or <see cref="Services.IOpenApiDocGenerator"/>.
/// </summary>
/// <param name="Written">The generated files that were written (relative paths).</param>
/// <param name="Skipped">Existing files that are not marked as generated and were left untouched (relative paths).</param>
/// <param name="Conflicts">Generated files whose content was edited since the last generation; the new content was written next to them with a <c>.regenerated</c> suffix (relative paths).</param>
/// <param name="Warnings">Problems found while reading the input.</param>
public sealed record DocGenReport(
    IReadOnlyList<string> Written,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Informational summary lines, for example how many non-public types and members were left out.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}
