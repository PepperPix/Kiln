namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class DeploymentInitializerTests
{
    private const string GitHubWorkflowRelativePath = ".github/workflows/deploy.yml";
    private const string AzureWorkflowRelativePath = ".github/workflows/azure-swa.yml";
    private const string AzureConfigRelativePath = "staticwebapp.config.json";
    private const int AzureCreatedFilesCount = 2;

    [Test]
    public async Task Initialize_GitHubPages_CreatesExpectedWorkflow()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-deploy-gh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var initializer = new DeploymentInitializer();

            var result = initializer.Initialize(DeploymentTarget.GitHubPages, dir);

            await Assert.That(result.Target).IsEqualTo(DeploymentTarget.GitHubPages);
            await Assert.That(result.CreatedFiles).Count().IsEqualTo(1);
            await Assert.That(result.CreatedFiles).Contains(GitHubWorkflowRelativePath);

            var workflowPath = Path.Combine(dir, ".github", "workflows", "deploy.yml");
            await Assert.That(File.Exists(workflowPath)).IsTrue();

            var workflow = await File.ReadAllTextAsync(workflowPath);
            await Assert.That(workflow).Contains("name: Deploy to GitHub Pages");
            await Assert.That(workflow).Contains("uses: actions/deploy-pages@v4");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Initialize_AzureStaticWebApps_CreatesWorkflowAndConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-deploy-swa-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var initializer = new DeploymentInitializer();

            var result = initializer.Initialize(DeploymentTarget.AzureStaticWebApps, dir);

            await Assert.That(result.Target).IsEqualTo(DeploymentTarget.AzureStaticWebApps);
            await Assert.That(result.CreatedFiles).Count().IsEqualTo(AzureCreatedFilesCount);
            await Assert.That(result.CreatedFiles).Contains(AzureWorkflowRelativePath);
            await Assert.That(result.CreatedFiles).Contains(AzureConfigRelativePath);

            var workflowPath = Path.Combine(dir, ".github", "workflows", "azure-swa.yml");
            var configPath = Path.Combine(dir, AzureConfigRelativePath);

            await Assert.That(File.Exists(workflowPath)).IsTrue();
            await Assert.That(File.Exists(configPath)).IsTrue();

            var workflow = await File.ReadAllTextAsync(workflowPath);
            var config = await File.ReadAllTextAsync(configPath);

            await Assert.That(workflow).Contains("name: Deploy to Azure Static Web Apps");
            await Assert.That(workflow).Contains("uses: Azure/static-web-apps-deploy@v1");
            await Assert.That(config).Contains("\"responseOverrides\"");
            await Assert.That(config).Contains("\"/404.html\"");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
