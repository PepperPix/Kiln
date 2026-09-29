namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class DeploymentInitializerSafetyTests
{
    private const string GitHubWorkflow = ".github/workflows/deploy.yml";
    private const string AzureWorkflow = ".github/workflows/azure-swa.yml";
    private const string AzureConfig = "staticwebapp.config.json";
    private const string UserContent = "# hand-written by the user\n";

    [Test]
    public async Task Initialize_ExistingFile_IsKeptAndReportedAsSkipped()
    {
        var dir = CreateProject();
        try
        {
            var path = WriteFile(dir, GitHubWorkflow, UserContent);

            var result = new DeploymentInitializer().Initialize(DeploymentTarget.GitHubPages, dir);

            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(UserContent);
            await Assert.That(result.CreatedFiles).IsEmpty();
            await Assert.That(result.SkippedFiles).Contains(GitHubWorkflow);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Initialize_ExistingFileWithForce_IsOverwritten()
    {
        var dir = CreateProject();
        try
        {
            var path = WriteFile(dir, GitHubWorkflow, UserContent);

            var result = new DeploymentInitializer().Initialize(
                DeploymentTarget.GitHubPages, dir, new DeploymentInitOptions(Force: true));

            await Assert.That(await File.ReadAllTextAsync(path)).Contains("name: Deploy to GitHub Pages");
            await Assert.That(result.CreatedFiles).Contains(GitHubWorkflow);
            await Assert.That(result.SkippedFiles).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Initialize_AzureWithOneExistingFile_SkipsOnlyThatFile()
    {
        var dir = CreateProject();
        try
        {
            var configPath = WriteFile(dir, AzureConfig, "{ \"custom\": true }");

            var result = new DeploymentInitializer().Initialize(DeploymentTarget.AzureStaticWebApps, dir);

            await Assert.That(await File.ReadAllTextAsync(configPath)).IsEqualTo("{ \"custom\": true }");
            await Assert.That(result.CreatedFiles).Contains(AzureWorkflow);
            await Assert.That(result.SkippedFiles).Contains(AzureConfig);
            await Assert.That(File.Exists(Path.Combine(dir, ".github", "workflows", "azure-swa.yml"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Initialize_ConfiguredOutputDir_IsUsedInGitHubWorkflow()
    {
        var dir = CreateProject("outputDir: dist\n");
        try
        {
            new DeploymentInitializer(new SiteConfigLoader()).Initialize(DeploymentTarget.GitHubPages, dir);

            var workflow = await File.ReadAllTextAsync(Path.Combine(dir, ".github", "workflows", "deploy.yml"));
            await Assert.That(workflow).Contains("path: dist\n");
            await Assert.That(workflow).DoesNotContain("_site");
            await Assert.That(workflow).DoesNotContain("@@");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Initialize_ConfiguredOutputDir_IsUsedInAzureWorkflow()
    {
        var dir = CreateProject("outputDir: build/site\n");
        try
        {
            new DeploymentInitializer(new SiteConfigLoader()).Initialize(DeploymentTarget.AzureStaticWebApps, dir);

            var workflow = await File.ReadAllTextAsync(Path.Combine(dir, ".github", "workflows", "azure-swa.yml"));
            await Assert.That(workflow).Contains("output_location: \"build/site\"");
            await Assert.That(workflow).DoesNotContain("_site");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Initialize_NoOutputDirConfigured_UsesDefault()
    {
        var dir = CreateProject();
        try
        {
            new DeploymentInitializer(new SiteConfigLoader()).Initialize(DeploymentTarget.GitHubPages, dir);

            var workflow = await File.ReadAllTextAsync(Path.Combine(dir, ".github", "workflows", "deploy.yml"));
            await Assert.That(workflow).Contains("path: _site\n");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Initialize_WithoutSiteYaml_UsesDefault()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-deploy-nosite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            new DeploymentInitializer(new SiteConfigLoader()).Initialize(DeploymentTarget.AzureStaticWebApps, dir);

            var workflow = await File.ReadAllTextAsync(Path.Combine(dir, ".github", "workflows", "azure-swa.yml"));
            await Assert.That(workflow).Contains("output_location: \"_site\"");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("\"dist\\n  evil: 1\"")]
    [Arguments("\"dist #comment\"")]
    [Arguments("\"a b\"")]
    [Arguments("\"dist:x\"")]
    public async Task Initialize_UnsafeOutputDir_ThrowsInsteadOfWritingIt(string yamlValue)
    {
        var dir = CreateProject($"outputDir: {yamlValue}\n");
        try
        {
            await Assert.That(() => new DeploymentInitializer(new SiteConfigLoader()).Initialize(DeploymentTarget.GitHubPages, dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("outputDir");
            await Assert.That(File.Exists(Path.Combine(dir, ".github", "workflows", "deploy.yml"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateProject(string extraSiteYaml = "")
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-deploy-safe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "site.yaml"),
            $"title: Test\nbaseUrl: https://example.com\n{extraSiteYaml}");
        return dir;
    }

    private static string WriteFile(string projectDir, string relativePath, string content)
    {
        var path = Path.Combine(projectDir, Path.Combine(relativePath.Split('/')));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
