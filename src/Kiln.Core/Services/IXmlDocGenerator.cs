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

    /// <summary>
    /// Generates one Markdown page per documented type, applying <paramref name="options"/>.
    /// </summary>
    /// <param name="xmlDocPath">The path of the XML documentation file.</param>
    /// <param name="outputDir">The directory the pages are written to.</param>
    /// <param name="options">The generation options. Implementations that do not support them ignore them.</param>
    /// <returns>A report of the files written, skipped and in conflict, and of warnings.</returns>
    /// <exception cref="FileNotFoundException">The XML documentation file or the assembly in <paramref name="options"/> does not exist.</exception>
    /// <exception cref="InvalidDataException">The assembly in <paramref name="options"/> is not a valid managed assembly.</exception>
    DocGenReport Generate(string xmlDocPath, string outputDir, XmlDocGenerationOptions options) =>
        Generate(xmlDocPath, outputDir);
}
