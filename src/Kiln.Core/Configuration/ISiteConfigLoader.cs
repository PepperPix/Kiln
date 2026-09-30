namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Loads the configuration of a site from its project directory.
/// </summary>
public interface ISiteConfigLoader
{
    /// <summary>
    /// Loads the site configuration from <c>site.yaml</c> (or <c>site.yml</c>) in the project directory.
    /// </summary>
    /// <param name="projectPath">The project directory.</param>
    /// <returns>The site configuration.</returns>
    /// <exception cref="FileNotFoundException">Neither <c>site.yaml</c> nor <c>site.yml</c> exists in the project directory.</exception>
    /// <exception cref="InvalidOperationException">A required field (<c>title</c> or <c>baseUrl</c>) is missing, or the <c>home</c> section is invalid.</exception>
    SiteConfiguration Load(string projectPath);
}
