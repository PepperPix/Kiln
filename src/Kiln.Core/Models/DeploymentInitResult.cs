namespace Kiln.Models;

/// <summary>
/// The outcome of <see cref="Services.IDeploymentInitializer"/>.
/// </summary>
/// <param name="Target">The deployment target that was initialized.</param>
/// <param name="CreatedFiles">The files that were written (relative paths).</param>
public sealed record DeploymentInitResult(DeploymentTarget Target, IReadOnlyList<string> CreatedFiles)
{
    /// <summary>Files that already existed and were left untouched (relative paths, '/' separated).</summary>
    public IReadOnlyList<string> SkippedFiles { get; init; } = [];
}
