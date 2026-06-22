namespace Kiln.Models;

public sealed record DeploymentInitResult(DeploymentTarget Target, IReadOnlyList<string> CreatedFiles);
