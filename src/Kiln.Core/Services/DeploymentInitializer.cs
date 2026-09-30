namespace Kiln.Services;

using System.Text;
using System.Text.RegularExpressions;
using Kiln.Models;
using YamlDotNet.Core;

/// <summary>
/// Default <see cref="IDeploymentInitializer"/> implementation that writes the deployment files for GitHub Pages and Azure Static Web Apps.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on <see cref="IDeploymentInitializer"/> instead.
/// </remarks>
public sealed partial class DeploymentInitializer : IDeploymentInitializer
{
    private const string DefaultOutputDir = "_site";
    private const string OutputDirPlaceholder = "@@OUTPUT_DIR@@";

    private readonly ISiteConfigLoader? _siteConfigLoader;

    public DeploymentInitializer()
    {
    }

    public DeploymentInitializer(ISiteConfigLoader siteConfigLoader)
    {
        _siteConfigLoader = siteConfigLoader;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeOutputDirRegex();

    public DeploymentInitResult Initialize(DeploymentTarget target, string projectPath, CancellationToken cancellationToken = default)
        => Initialize(target, projectPath, new DeploymentInitOptions(), cancellationToken);

    public DeploymentInitResult Initialize(
        DeploymentTarget target,
        string projectPath,
        DeploymentInitOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        return target switch
        {
            DeploymentTarget.GitHubPages => InitGitHubPages(projectPath, options),
            DeploymentTarget.AzureStaticWebApps => InitAzureSwa(projectPath, options),
            _ => throw new InvalidOperationException($"Unsupported deployment target: {target}"),
        };
    }

    private string ResolveOutputDir(string projectPath)
    {
        if (_siteConfigLoader is null)
            return DefaultOutputDir;

        string configured;
        try
        {
            configured = _siteConfigLoader.Load(projectPath).OutputDir;
        }
        catch (FileNotFoundException)
        {
            return DefaultOutputDir;
        }
        catch (InvalidOperationException)
        {
            return DefaultOutputDir;
        }
        catch (YamlException)
        {
            return DefaultOutputDir;
        }

        var normalized = configured.Replace('\\', '/').Trim().TrimEnd('/');
        if (normalized.Length == 0)
            return DefaultOutputDir;

        if (!SafeOutputDirRegex().IsMatch(normalized))
            throw new InvalidOperationException(
                $"site.yaml 'outputDir' value '{configured}' contains characters that are not allowed in a deployment workflow (allowed: letters, digits, '.', '_', '-', '/').");

        return normalized;
    }

    private static void WriteFile(
        string projectPath,
        string relativePath,
        string content,
        bool force,
        List<string> created,
        List<string> skipped)
    {
        var fullPath = Path.Combine(projectPath, Path.Combine(relativePath.Split('/')));
        if (File.Exists(fullPath) && !force)
        {
            skipped.Add(relativePath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, Encoding.UTF8);
        created.Add(relativePath);
    }

    private DeploymentInitResult InitGitHubPages(string projectPath, DeploymentInitOptions options)
    {
        var outputDir = ResolveOutputDir(projectPath);

        const string workflow = """
name: Deploy to GitHub Pages

on:
  push:
    branches: [main]

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore tools
        run: dotnet tool restore

      - name: Build site
        run: dotnet tool run kiln build --release --base-url ${{ vars.SITE_URL || 'https://username.github.io/repo' }}

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: @@OUTPUT_DIR@@

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
""";

        var created = new List<string>();
        var skipped = new List<string>();
        WriteFile(projectPath, ".github/workflows/deploy.yml", workflow.Replace(OutputDirPlaceholder, outputDir, StringComparison.Ordinal), options.Force, created, skipped);

        return new DeploymentInitResult(DeploymentTarget.GitHubPages, created) { SkippedFiles = skipped };
    }

    private DeploymentInitResult InitAzureSwa(string projectPath, DeploymentInitOptions options)
    {
        var outputDir = ResolveOutputDir(projectPath);

        const string workflow = """
name: Deploy to Azure Static Web Apps

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build and Deploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          app_location: "/"
          output_location: "@@OUTPUT_DIR@@"
          app_build_command: "dotnet tool restore && dotnet tool run kiln build --release"
          skip_api_build: true
""";

        const string config = """
{
  "responseOverrides": {
    "404": { "rewrite": "/404.html" }
  },
  "routes": [
    {
      "route": "/assets/*",
      "headers": {
        "Cache-Control": "public, max-age=31536000, immutable"
      }
    }
  ]
}
""";

        var created = new List<string>();
        var skipped = new List<string>();
        WriteFile(projectPath, ".github/workflows/azure-swa.yml", workflow.Replace(OutputDirPlaceholder, outputDir, StringComparison.Ordinal), options.Force, created, skipped);
        WriteFile(projectPath, "staticwebapp.config.json", config, options.Force, created, skipped);

        return new DeploymentInitResult(DeploymentTarget.AzureStaticWebApps, created) { SkippedFiles = skipped };
    }
}
