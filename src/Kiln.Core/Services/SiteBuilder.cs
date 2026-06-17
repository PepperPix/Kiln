namespace Kiln.Services;

using System.Collections.ObjectModel;
using System.Diagnostics;
using Kiln.Models;

public sealed class SiteBuilder(
    IContentReader contentReader,
    ITemplateRenderer templateRenderer,
    ISiteConfigLoader configLoader) : ISiteBuilder
{
    public async Task<BuildResult> BuildAsync(string projectPath, bool includeDrafts = false, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new Collection<string>();
        var errors = new Collection<string>();

        // Load configuration
        var config = configLoader.Load(projectPath);
        var contentDir = Path.Combine(projectPath, config.ContentDir);
        var outputDir = Path.Combine(projectPath, config.OutputDir);
        var themePath = Path.Combine(projectPath, config.ThemesDir, config.Theme);

        if (!Directory.Exists(themePath))
        {
            errors.Add($"Theme directory not found: {themePath}");
            return MakeResult(0, 0, 0, stopwatch.Elapsed, outputDir, warnings, errors);
        }

        // Read content
        var items = contentReader.ReadAll(contentDir, config.OutputDir);
        var skippedDrafts = 0;

        // Clean output directory
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
        Directory.CreateDirectory(outputDir);

        var rendered = 0;

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            if (item.FrontMatter.Draft && !includeDrafts)
            {
                skippedDrafts++;
                continue;
            }

            try
            {
                var html = templateRenderer.Render(item, config, themePath);
                var outputPath = Path.Combine(outputDir, item.OutputPath);
                var outputDirectory = Path.GetDirectoryName(outputPath)!;

                Directory.CreateDirectory(outputDirectory);
                await File.WriteAllTextAsync(outputPath, html, ct).ConfigureAwait(false);
                rendered++;
            }
#pragma warning disable CA1031 // Intentional: one file error should not abort the entire build
            catch (Exception ex)
#pragma warning restore CA1031
            {
                errors.Add($"Error rendering '{item.RelativePath}': {ex.Message}");
            }
        }

        // Copy static assets from theme
        var staticDir = Path.Combine(themePath, "static");
        if (Directory.Exists(staticDir))
            CopyDirectory(staticDir, outputDir);

        stopwatch.Stop();
        return MakeResult(items.Count, rendered, skippedDrafts, stopwatch.Elapsed, outputDir, warnings, errors);
    }

    private static BuildResult MakeResult(int total, int rendered, int skipped, TimeSpan duration, string outputDir, Collection<string> warnings, Collection<string> errors)
    {
        return new BuildResult
        {
            TotalFiles = total,
            RenderedFiles = rendered,
            SkippedDrafts = skipped,
            Duration = duration,
            OutputDirectory = outputDir,
            Warnings = warnings,
            Errors = errors
        };
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
        }
    }
}
