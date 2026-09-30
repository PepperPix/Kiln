namespace Kiln.Models;

/// <summary>
/// The outcome of creating a new site with <see cref="Services.IScaffolder"/>.
/// </summary>
/// <param name="ProjectPath">The full path of the created project directory.</param>
/// <param name="CreatedFiles">The files that were written (relative paths, '/' separated).</param>
public sealed record ScaffoldResult(string ProjectPath, IReadOnlyList<string> CreatedFiles);
