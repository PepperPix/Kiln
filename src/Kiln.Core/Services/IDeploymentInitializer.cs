namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Creates the files needed to deploy a site to a hosting target.
/// </summary>
public interface IDeploymentInitializer
{
    /// <summary>Creates the deployment files for <paramref name="target"/>; existing files are left untouched.</summary>
    DeploymentInitResult Initialize(DeploymentTarget target, string projectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the deployment files for <paramref name="target"/> honoring <paramref name="options"/>.
    /// Implementations that predate the options fall back to <see cref="Initialize(DeploymentTarget, string, CancellationToken)"/>.
    /// </summary>
    DeploymentInitResult Initialize(
        DeploymentTarget target,
        string projectPath,
        DeploymentInitOptions options,
        CancellationToken cancellationToken = default)
        => Initialize(target, projectPath, cancellationToken);
}
