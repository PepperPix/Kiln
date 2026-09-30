namespace Kiln.Models;

/// <summary>
/// A Markdown page produced by a documentation generator, to be written by <see cref="Services.IGeneratedContentWriter"/>.
/// </summary>
/// <param name="RelativePath">The path of the file relative to the output directory, using <c>/</c> as separator.</param>
/// <param name="FrontMatter">The front matter entries in output order.</param>
/// <param name="Body">The Markdown body.</param>
public sealed record GeneratedContentFile(
    string RelativePath,
    IReadOnlyList<(string Key, object Value)> FrontMatter,
    string Body);
