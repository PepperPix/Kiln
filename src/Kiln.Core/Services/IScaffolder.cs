namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Creates new site projects.
/// </summary>
public interface IScaffolder
{
    /// <summary>
    /// Creates a new site project, including sample content and the default theme.
    /// </summary>
    /// <param name="name">The name of the site, which is also the name of the project directory.</param>
    /// <param name="outputDirectory">The directory in which the project directory is created.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The path of the project and the files that were created.</returns>
    /// <exception cref="InvalidOperationException">The project directory already exists and is not empty.</exception>
    ScaffoldResult CreateSite(string name, string outputDirectory, CancellationToken cancellationToken = default);
}
