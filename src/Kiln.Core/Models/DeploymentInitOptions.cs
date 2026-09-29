namespace Kiln.Models;

/// <summary>Options for <see cref="Services.IDeploymentInitializer"/>.</summary>
/// <param name="Force">Overwrite deployment files that already exist.</param>
public sealed record DeploymentInitOptions(bool Force = false);
