namespace Kiln.Models;

public sealed record DeploymentInitResult(DeploymentTarget Target, IReadOnlyList<string> CreatedFiles)
{
    /// <summary>Files that already existed and were left untouched (relative paths, '/' separated).</summary>
    public IReadOnlyList<string> SkippedFiles { get; init; } = [];
}
