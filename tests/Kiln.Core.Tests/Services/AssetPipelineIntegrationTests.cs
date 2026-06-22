namespace Kiln.Core.Tests.Services;

using System.Text;
using System.Text.RegularExpressions;
using Kiln.Abstractions;
using Kiln.Services;

public class AssetPipelineIntegrationTests
{
    private const string SiteCssRelativePath = "themes/default/static/css/site.css";
    private const string SiteCssAssetPath = "/assets/css/site.css";
    private const string DeadLinkPath = "/does-not-exist/";
    private static readonly Regex FingerprintedCssRegex = new("^site\\.[0-9a-f]{8}\\.css$", RegexOptions.Compiled);

    [Test]
    public async Task BuildAsync_Production_MinifiesFingerprintsAndRewritesReferences()
    {
        var dir = CreateSite();

        try
        {
            var sourceCssPath = Path.Combine(dir, SiteCssRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourceCssLength = new FileInfo(sourceCssPath).Length;

            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var cssOutputDir = Path.Combine(dir, "_site", "assets", "css");
            var hashedCssFiles = Directory.GetFiles(cssOutputDir, "site.*.css");
            await Assert.That(hashedCssFiles).Count().IsEqualTo(1);

            var hashedCssFileName = Path.GetFileName(hashedCssFiles[0]);
            await Assert.That(FingerprintedCssRegex.IsMatch(hashedCssFileName)).IsTrue();
            await Assert.That(File.Exists(Path.Combine(cssOutputDir, "site.css"))).IsFalse();

            var minifiedCssLength = new FileInfo(hashedCssFiles[0]).Length;
            await Assert.That(minifiedCssLength).IsLessThan(sourceCssLength);

            var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            await Assert.That(postHtml).Contains($"/assets/css/{hashedCssFileName}");
            await Assert.That(postHtml).DoesNotContain(SiteCssAssetPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_FailsOnDeadInternalLink()
    {
        var dir = CreateSite(includeDeadLink: true);

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(static error =>
                    error.Contains(DeadLinkPath, StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Development_LeavesAssetsUnchangedAndIgnoresDeadLinks()
    {
        var dir = CreateSite(includeDeadLink: true);

        try
        {
            var sourceCssPath = Path.Combine(dir, SiteCssRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourceCss = await File.ReadAllTextAsync(sourceCssPath);

            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Development, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var cssOutputDir = Path.Combine(dir, "_site", "assets", "css");
            var plainCssPath = Path.Combine(cssOutputDir, "site.css");
            await Assert.That(File.Exists(plainCssPath)).IsTrue();
            await Assert.That(Directory.GetFiles(cssOutputDir, "site.*.css")).Count().IsEqualTo(0);

            var outputCss = await File.ReadAllTextAsync(plainCssPath);
            await Assert.That(outputCss).IsEqualTo(sourceCss);

            var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            await Assert.That(postHtml).Contains(SiteCssAssetPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_FingerprintIsDeterministic()
    {
        var dir = CreateSite();

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);

            var firstResult = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(firstResult.Success).IsTrue();
            var firstHashFileName = GetSingleFingerprintedCssFileName(Path.Combine(dir, "_site", "assets", "css"));

            var secondResult = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(secondResult.Success).IsTrue();
            var secondHashFileName = GetSingleFingerprintedCssFileName(Path.Combine(dir, "_site", "assets", "css"));

            await Assert.That(secondHashFileName).IsEqualTo(firstHashFileName);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_UnknownMinifierIdReturnsError()
    {
        var dir = CreateSite(minifierId: "doesnotexist");

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(static error =>
                    error.Contains("Unknown asset minifier id 'doesnotexist'", StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(result.Errors.Any(static error =>
                    error.Contains("'nuglify'", StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_FingerprintDisabled_KeepsPlainCssFile()
    {
        var dir = CreateSite(fingerprint: false);

        try
        {
            var sourceCssPath = Path.Combine(dir, SiteCssRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourceCssLength = new FileInfo(sourceCssPath).Length;

            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var cssOutputDir = Path.Combine(dir, "_site", "assets", "css");
            var plainCssPath = Path.Combine(cssOutputDir, "site.css");
            await Assert.That(File.Exists(plainCssPath)).IsTrue();

            var allCssFileNames = Directory.EnumerateFiles(cssOutputDir, "*.css")
                .Select(Path.GetFileName)
                .Where(static name => name is not null)
                .Cast<string>()
                .ToArray();
            await Assert.That(allCssFileNames.Any(FingerprintedCssRegex.IsMatch)).IsFalse();

            var minifiedCssLength = new FileInfo(plainCssPath).Length;
            await Assert.That(minifiedCssLength).IsLessThan(sourceCssLength);

            var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            await Assert.That(postHtml).Contains(SiteCssAssetPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_LinkCheckDisabled_AllowsDeadInternalLink()
    {
        var dir = CreateSite(includeDeadLink: true, linkCheck: false);

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Errors).Count().IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateSite(
        bool includeDeadLink = false,
        bool fingerprint = true,
        bool linkCheck = true,
        string minifierId = "nuglify")
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-asset-pipeline-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "static", "css"));

        var siteYaml = new StringBuilder()
            .AppendLine("title: Test Site")
            .AppendLine("baseUrl: http://localhost:5555")
            .AppendLine("theme: default")
            .AppendLine("assets:")
            .AppendLine($"  minifier: {minifierId}")
            .AppendLine("build:")
            .AppendLine($"  fingerprint: {fingerprint.ToString().ToLowerInvariant()}")
            .AppendLine($"  linkCheck: {linkCheck.ToString().ToLowerInvariant()}")
            .AppendLine("collections:")
            .AppendLine("  posts:")
            .AppendLine("    directory: content/posts")
            .AppendLine("    permalink: /blog/:slug/")
            .ToString();

        File.WriteAllText(Path.Combine(dir, "site.yaml"), siteYaml);

        var layoutDeadLink = includeDeadLink
            ? $"<a href=\"{DeadLinkPath}\">broken</a>"
            : string.Empty;

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            date: 2026-01-01
            ---
            Post content.
            """);

                const string defaultLayout =
                        """
                        <html>
                            <head>
                                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
                            </head>
                            <body>
                                __DEAD_LINK__
                                {{ page.content }}
                            </body>
                        </html>
                        """;
                File.WriteAllText(
                        Path.Combine(dir, "themes", "default", "layouts", "default.html"),
                        defaultLayout.Replace("__DEAD_LINK__", layoutDeadLink, StringComparison.Ordinal));

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "post.html"),
            """
            <html>
              <head>
                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
              </head>
              <body>
                {{ page.content }}
              </body>
            </html>
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "posts-index.html"),
            """
            <html>
              <head>
                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
              </head>
              <body>
                {{ for item in paginator.items }}<a href="{{ item.url }}">{{ item.title }}</a>{{ end }}
              </body>
            </html>
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            """
            <html>
              <head>
                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
              </head>
              <body>Not found.</body>
            </html>
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "static", "css", "site.css"),
            """
            /* Integration test CSS with intentional whitespace for minification checks */
            body {
                margin: 0;
                padding: 0;
                color:   #ffffff;
                background-color: #101010;
            }

            .content {
                max-width: 960px;
                margin: 0 auto;
            }
            """);

        return dir;
    }

    private static ISiteBuilder CreateBuilder(IEnumerable<IAssetMinifier> minifiers)
    {
        var markdownProcessor = new MarkdownProcessor();
        var contentReader = new ContentReader(markdownProcessor);
        var templateRenderer = new TemplateRenderer();
        var permalinkGenerator = new PermalinkGenerator();
        var configLoader = new SiteConfigLoader();
        var pluginLoader = new PluginLoader();
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, minifiers);
    }

    private static string GetSingleFingerprintedCssFileName(string cssOutputDir)
    {
        var fileNames = Directory.EnumerateFiles(cssOutputDir, "*.css")
            .Select(Path.GetFileName)
            .Where(static name => name is not null)
            .Cast<string>()
            .Where(static name => FingerprintedCssRegex.IsMatch(name))
            .ToArray();

        if (fileNames.Length != 1)
            throw new InvalidOperationException("Expected exactly one fingerprinted CSS file.");

        return fileNames[0];
    }
}