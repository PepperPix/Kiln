namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Writes generated Markdown pages while protecting pages that were edited by hand.
/// </summary>
public interface IGeneratedContentWriter
{
    /// <summary>
    /// Writes a generated page to the output directory.
    /// </summary>
    /// <param name="outputDir">The output directory.</param>
    /// <param name="file">The page to write.</param>
    /// <returns>What happened to the file on disk; see <see cref="WriteResult"/>.</returns>
    WriteResult Write(string outputDir, GeneratedContentFile file);
}
