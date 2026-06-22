namespace Kiln.Models;

public sealed record ScaffoldResult(string ProjectPath, IReadOnlyList<string> CreatedFiles);
