namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Generates Markdown reference pages from an OpenAPI specification.
/// </summary>
public interface IOpenApiDocGenerator
{
    /// <summary>
    /// Generates one Markdown page per operation of the specification.
    /// </summary>
    /// <param name="specPath">The path of the OpenAPI specification.</param>
    /// <param name="outputDir">The directory the pages are written to.</param>
    /// <returns>A report of the files written, skipped and in conflict, and of warnings.</returns>
    /// <exception cref="FileNotFoundException">The specification file does not exist.</exception>
    DocGenReport Generate(string specPath, string outputDir);
}
