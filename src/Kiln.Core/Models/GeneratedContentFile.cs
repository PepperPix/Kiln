namespace Kiln.Models;

public sealed record GeneratedContentFile(
    string RelativePath,
    IReadOnlyList<(string Key, object Value)> FrontMatter,
    string Body);
