namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class ApiThemeLayoutTests
{
    private static ISiteBuilder CreateBuilder()
    {
        var markdownProcessor = new MarkdownProcessor();
        var contentReader = new ContentReader(markdownProcessor);
        var templateRenderer = new TemplateRenderer();
        var permalinkGenerator = new PermalinkGenerator();
        var configLoader = new SiteConfigLoader();
        var pluginLoader = new PluginLoader();
        return new SiteBuilder(
            contentReader,
            templateRenderer,
            permalinkGenerator,
            configLoader,
            pluginLoader,
            [],
            new SkiaSharpImageOptimizer(),
            new AssetReferenceIndexBuilder());
    }

    [Test]
    public async Task Build_ScaffoldedSiteWithoutApiCollection_DoesNotReferenceApiCssInHtml()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-scaffold-plain-{Guid.NewGuid():N}");
        try
        {
            var scaffolder = new Scaffolder();
            var scaffoldResult = scaffolder.CreateSite("demo", tempRoot);
            var projectDir = scaffoldResult.ProjectPath;

            var builder = CreateBuilder();
            var buildResult = await builder.BuildAsync(projectDir);

            await Assert.That(buildResult.Success).IsTrue();

            var apiCssPath = Path.Combine(projectDir, "_site", "assets", "css", "api.css");
            await Assert.That(File.Exists(apiCssPath)).IsTrue();

            var htmlFiles = Directory.GetFiles(Path.Combine(projectDir, "_site"), "*.html", SearchOption.AllDirectories);
            await Assert.That(htmlFiles.Length).IsGreaterThan(0);

            foreach (var htmlFile in htmlFiles)
            {
                var htmlContent = await File.ReadAllTextAsync(htmlFile);
                await Assert.That(htmlContent).DoesNotContain("api.css");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Test]
    public async Task Build_ScaffoldedSiteWithApiCollection_RendersSidebarBreadcrumbsBadgesAndVersion()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-scaffold-api-{Guid.NewGuid():N}");
        try
        {
            var scaffolder = new Scaffolder();
            var scaffoldResult = scaffolder.CreateSite("demo", tempRoot);
            var projectDir = scaffoldResult.ProjectPath;

            var siteYamlPath = Path.Combine(projectDir, "site.yaml");
            var siteYaml = await File.ReadAllTextAsync(siteYamlPath);
            var updatedSiteYaml = siteYaml.Replace("  pages:", """
  reference:
    directory: content/reference
    permalink: /reference/:slug/
    sort: weight asc
    layout: api

  pages:
""", StringComparison.Ordinal);
            await File.WriteAllTextAsync(siteYamlPath, updatedSiteYaml);

            var refDir = Path.Combine(projectDir, "content", "reference");
            var servicesDir = Path.Combine(refDir, "Kiln", "Services");
            var modelsDir = Path.Combine(refDir, "Kiln", "Models");
            Directory.CreateDirectory(servicesDir);
            Directory.CreateDirectory(modelsDir);

            await File.WriteAllTextAsync(Path.Combine(refDir, "index.md"),
                """
                ---
                title: Reference Overview
                url: /reference/
                weight: 0
                ---
                Overview of Kiln Reference.
                """);

            await File.WriteAllTextAsync(Path.Combine(servicesDir, "SiteBuilder.md"),
                """
                ---
                title: SiteBuilder
                weight: 1
                generated: true
                extra:
                  namespace: Kiln.Services
                  assembly: Kiln.Core
                  version: 1.3.0-beta.5
                  kind: class
                source_hash: hash1
                ---
                # SiteBuilder
                The main site builder.
                """);

            await File.WriteAllTextAsync(Path.Combine(modelsDir, "BuildResult.md"),
                """
                ---
                title: BuildResult
                weight: 2
                generated: true
                extra:
                  namespace: Kiln.Models
                  assembly: Kiln.Core
                  version: 1.3.0-beta.5
                  kind: record
                source_hash: hash2
                ---
                # BuildResult
                The outcome of a build.
                """);

            var builder = CreateBuilder();
            var buildResult = await builder.BuildAsync(projectDir);

            await Assert.That(buildResult.Success).IsTrue();

            var apiCssPath = Path.Combine(projectDir, "_site", "assets", "css", "api.css");
            await Assert.That(File.Exists(apiCssPath)).IsTrue();

            var siteBuilderHtmlPath = Path.Combine(projectDir, "_site", "reference", "Kiln", "Services", "SiteBuilder", "index.html");
            await Assert.That(File.Exists(siteBuilderHtmlPath)).IsTrue();
            var siteBuilderHtml = await File.ReadAllTextAsync(siteBuilderHtmlPath);

            await Assert.That(siteBuilderHtml).Contains("class=\"api-nav\"");
            await Assert.That(siteBuilderHtml).Contains("href=\"/reference/Kiln/Services/SiteBuilder/\" aria-current=\"page\"");
            await Assert.That(siteBuilderHtml).Contains("<a href=\"/reference/\">reference</a>");
            await Assert.That(siteBuilderHtml).Contains("<span class=\"api-kind\">class</span>");
            await Assert.That(siteBuilderHtml).Contains("Package: Kiln.Core 1.3.0-beta.5");
            await Assert.That(siteBuilderHtml).Contains("<details open><summary>Kiln.Services</summary>");
            await Assert.That(siteBuilderHtml).Contains("<details><summary>Kiln.Models</summary>");
            await Assert.That(siteBuilderHtml).DoesNotContain("<details open><summary>Kiln.Models</summary>");
            await Assert.That(siteBuilderHtml).Contains("<link rel=\"stylesheet\" href=\"/assets/css/api.css\">");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }
}
