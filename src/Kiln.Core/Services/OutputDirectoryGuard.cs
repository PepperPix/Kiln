namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Ensures the configured output directory is a real subdirectory of the project that does not overlap any source directory,
/// because a build deletes and rewrites everything below it.
/// </summary>
internal static class OutputDirectoryGuard
{
    private static readonly string[] FixedSourceDirectories = ["static", "plugins", ".git", ".kiln"];

    /// <summary>Returns <c>null</c> when <c>outputDir</c> is safe, otherwise a message describing the violation.</summary>
    internal static string? Validate(string projectPath, SiteConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.OutputDir))
            return "site.yaml 'outputDir' must not be empty; it must be a subdirectory of the project (for example '_site').";

        string projectRoot;
        string outputDir;
        try
        {
            projectRoot = PathContainment.Normalize(projectPath);
            outputDir = PathContainment.Normalize(Path.Combine(projectPath, config.OutputDir));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"site.yaml 'outputDir' value '{config.OutputDir}' is not a valid path: {ex.Message}";
        }

        if (!PathContainment.IsDescendant(projectRoot, outputDir))
        {
            return $"site.yaml 'outputDir' resolves to '{outputDir}', which is not a subdirectory of the project '{projectRoot}'. "
                + "The output directory is deleted on every build and must be a real subdirectory such as '_site'.";
        }

        foreach (var (label, sourceDir) in EnumerateSourceDirectories(projectRoot, config))
        {
            // A source directory equal to the project root (e.g. a root-level home.page) contains every output directory by definition.
            if (PathContainment.AreSame(sourceDir, projectRoot))
                continue;

            if (PathContainment.AreSame(sourceDir, outputDir)
                || PathContainment.IsDescendant(sourceDir, outputDir)
                || PathContainment.IsDescendant(outputDir, sourceDir))
            {
                return $"site.yaml 'outputDir' resolves to '{outputDir}', which overlaps the {label} directory '{sourceDir}'. "
                    + "Choose a separate directory such as '_site'.";
            }
        }

        return null;
    }

    /// <summary>Throws when <paramref name="outputDir"/> is the project root or one of its ancestors.</summary>
    internal static void EnsureNotProjectRootOrAncestor(string projectPath, string outputDir)
    {
        var projectRoot = PathContainment.Normalize(projectPath);
        var normalizedOutput = PathContainment.Normalize(outputDir);
        if (PathContainment.IsSameOrDescendant(normalizedOutput, projectRoot))
        {
            throw new InvalidOperationException(
                $"Refusing to modify output directory '{normalizedOutput}': it is the project root or one of its ancestors.");
        }
    }

    private static IEnumerable<(string Label, string Path)> EnumerateSourceDirectories(string projectRoot, SiteConfiguration config)
    {
        yield return ("themes", ResolveBelow(projectRoot, config.ThemesDir));

        foreach (var name in FixedSourceDirectories)
            yield return (name, ResolveBelow(projectRoot, name));

        foreach (var collection in config.Collections.Values)
        {
            if (string.IsNullOrWhiteSpace(collection.Directory))
                continue;

            yield return ($"collection '{collection.Name}'", ResolveBelow(projectRoot, collection.Directory));
        }

        if (!string.IsNullOrWhiteSpace(config.Home?.Page))
        {
            var pageDir = Path.GetDirectoryName(ResolveBelow(projectRoot, config.Home.Page));
            if (!string.IsNullOrEmpty(pageDir))
                yield return ("home.page", PathContainment.Normalize(pageDir));
        }
    }

    private static string ResolveBelow(string projectRoot, string relativeOrAbsolute)
    {
        try
        {
            return PathContainment.Normalize(Path.Combine(projectRoot, relativeOrAbsolute));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return projectRoot;
        }
    }
}
