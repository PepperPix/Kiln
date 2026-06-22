namespace Kiln.Services;

using Kiln.Models;

public interface IDeploymentInitializer
{
    DeploymentInitResult Initialize(DeploymentTarget target, string projectPath, CancellationToken cancellationToken = default);
}
