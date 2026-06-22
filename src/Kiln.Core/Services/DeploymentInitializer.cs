namespace Kiln.Services;

using System.Text;
using Kiln.Models;

public sealed class DeploymentInitializer : IDeploymentInitializer
{
    public DeploymentInitResult Initialize(DeploymentTarget target, string projectPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return target switch
        {
            DeploymentTarget.GitHubPages => InitGitHubPages(projectPath),
            DeploymentTarget.AzureStaticWebApps => InitAzureSwa(projectPath),
            _ => throw new InvalidOperationException($"Unsupported deployment target: {target}"),
        };
    }

    private static DeploymentInitResult InitGitHubPages(string projectPath)
    {
        var workflowPath = Path.Combine(projectPath, ".github", "workflows", "deploy.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(workflowPath)!);

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
        run: kiln build --base-url ${{ vars.SITE_URL || 'https://username.github.io/repo' }}

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: _site

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
""";

        File.WriteAllText(workflowPath, workflow, Encoding.UTF8);

        return new DeploymentInitResult(DeploymentTarget.GitHubPages, [".github/workflows/deploy.yml"]);
    }

    private static DeploymentInitResult InitAzureSwa(string projectPath)
    {
        var workflowPath = Path.Combine(projectPath, ".github", "workflows", "azure-swa.yml");
        var configPath = Path.Combine(projectPath, "staticwebapp.config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(workflowPath)!);

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
          output_location: "_site"
          app_build_command: "dotnet tool restore && kiln build"
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

        File.WriteAllText(workflowPath, workflow, Encoding.UTF8);
        File.WriteAllText(configPath, config, Encoding.UTF8);

        return new DeploymentInitResult(DeploymentTarget.AzureStaticWebApps, [
            ".github/workflows/azure-swa.yml",
            "staticwebapp.config.json",
        ]);
    }
}
