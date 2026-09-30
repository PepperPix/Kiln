namespace Kiln.Models;

/// <summary>
/// A hosting target for which deployment files can be generated.
/// </summary>
public enum DeploymentTarget
{
    /// <summary>GitHub Pages.</summary>
    GitHubPages,

    /// <summary>Azure Static Web Apps.</summary>
    AzureStaticWebApps,
}
