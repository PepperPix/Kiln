namespace Kiln.Services;

/// <summary>
/// The outcome of writing a generated page with <see cref="IGeneratedContentWriter"/>.
/// </summary>
public enum WriteResult
{
    /// <summary>The page was written, either as a new file or over an unmodified generated file.</summary>
    Written,

    /// <summary>A file already exists that is not marked as generated; it was left untouched.</summary>
    SkippedAdopted,

    /// <summary>The existing generated file was edited since it was generated; the new content was written to a file with a <c>.regenerated</c> suffix instead.</summary>
    Conflict,
}
