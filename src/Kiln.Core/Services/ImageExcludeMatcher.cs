namespace Kiln.Services;

using Microsoft.Extensions.FileSystemGlobbing;

/// <summary>
/// Matches a source file's project-relative path against the <c>images.exclude</c> glob
/// patterns from <c>site.yaml</c>, used to opt individual images out of optimization.
/// </summary>
internal static class ImageExcludeMatcher
{
    public static bool IsExcluded(string sourceFile, string projectPath, IReadOnlyList<string> excludePatterns)
    {
        if (excludePatterns.Count == 0) return false;

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(excludePatterns);

        var relative = Path.GetRelativePath(projectPath, sourceFile).Replace(Path.DirectorySeparatorChar, '/');
        return matcher.Match(relative).HasMatches;
    }
}
