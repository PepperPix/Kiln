namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Generates Markdown reference pages from a compiler-generated XML documentation file.
/// </summary>
public interface IXmlDocGenerator
{
    /// <summary>
    /// Generates one Markdown page per documented type.
    /// </summary>
    /// <param name="xmlDocPath">The path of the XML documentation file.</param>
    /// <param name="outputDir">The directory the pages are written to.</param>
    /// <returns>A report of the files written, skipped and in conflict, and of warnings.</returns>
    /// <exception cref="FileNotFoundException">The XML documentation file does not exist.</exception>
    DocGenReport Generate(string xmlDocPath, string outputDir);
}
