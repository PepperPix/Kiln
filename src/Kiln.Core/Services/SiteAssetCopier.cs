namespace Kiln.Services;

using System.Collections.ObjectModel;
using Kiln.Abstractions;
using Kiln.Models;

internal sealed class SiteAssetCopier(IImageOptimizer imageOptimizer, IAssetReferenceIndexBuilder assetReferenceIndexBuilder)
{
    internal void Copy(
        List<ContentItem> allItems,
        string projectPath,
        BuildEnvironment environment,
        Collection<string> warnings,
        RenderPassContext render)
    {
        var config = render.Config;
        var outputDir = render.OutputDir;
        var generatedFiles = render.GeneratedFiles;

        // Copy static assets from theme → _site/assets/ (lowest priority)
        var assetsOutputDir = Path.Combine(outputDir, "assets");
        var themeStaticDir = Path.Combine(render.ThemePath, "static");
        if (Directory.Exists(themeStaticDir))
            CopyDirectory(themeStaticDir, assetsOutputDir, generatedFiles);

        // Image optimization (Production only): only Site static/ and Page Bundle assets are
        // candidates, and only when actually referenced via <img src="/assets/..."> in already-
        // rendered HtmlContent. Theme/plugin static/ above and below are never optimized.
        var referencedImages = config.Images.Enabled && environment == BuildEnvironment.Production
            ? assetReferenceIndexBuilder.Build(allItems).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        var imageRenameManifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var imageCopyContext = new ImageCopyContext(outputDir, projectPath, config.Images, referencedImages, imageRenameManifest);

        // Copy static assets from site → _site/assets/ (overrides theme, warns on collision)
        var siteStaticDir = Path.Combine(projectPath, "static");
        if (Directory.Exists(siteStaticDir))
            CopyDirectoryWithCollisionWarning(siteStaticDir, assetsOutputDir, config.Theme, warnings, generatedFiles, imageCopyContext);

        // Copy co-located assets from Page Bundles → _site/assets/content/<collection>/<sectionPath>/<slug>/
        foreach (var item in allItems.Where(static i => i.AssetDirectory is not null))
        {
            var slugPath = string.IsNullOrEmpty(item.SectionPath) ? item.Slug : $"{item.SectionPath}/{item.Slug}";
            var destDir = Path.Combine(assetsOutputDir, "content", item.Collection.Name, slugPath);
            // Note: item.ImageOptimization == false already excluded this item's images from
            // 'referencedImages' above, so CopyOrOptimizeFile naturally skips optimizing them.
            CopyNonMarkdownFiles(item.AssetDirectory!, destDir, generatedFiles, imageCopyContext, warnings);
        }

        // Copy plugin assets: plugins/<name>/static/ → _site/assets/plugins/<name>/
        foreach (var plugin in render.Plugins)
        {
            var pluginStaticDir = Path.Combine(plugin.Directory, "static");
            if (!Directory.Exists(pluginStaticDir)) continue;
            var pluginKey = Path.GetFileName(plugin.Directory);
            var pluginAssetsDir = Path.Combine(assetsOutputDir, "plugins", pluginKey);
            CopyDirectory(pluginStaticDir, pluginAssetsDir, generatedFiles);
        }

        // Rewrite HTML/CSS references for any images renamed by optimization (e.g. WebP
        // conversion changing the extension) — must run before the fingerprint stage below,
        // so hashes cover the final on-disk bytes.
        if (imageRenameManifest.Count > 0)
            AssetPipeline.RewriteReferences(outputDir, imageRenameManifest);
    }

    private static void CopyDirectory(string sourceDir, string destDir, HashSet<string>? generatedFiles)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, overwrite: true);
            generatedFiles?.Add(Path.GetFullPath(destPath));
        }
    }

    private void CopyDirectoryWithCollisionWarning(
        string sourceDir,
        string destDir,
        string themeName,
        Collection<string> warnings,
        HashSet<string>? generatedFiles,
        ImageCopyContext imageCopyContext)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);

            if (File.Exists(destPath))
                warnings.Add($"Asset '{relativePath}' in static/ overrides same file from theme '{themeName}'");

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            CopyOrOptimizeFile(file, destPath, imageCopyContext, warnings);
            generatedFiles?.Add(Path.GetFullPath(destPath));
        }
    }

    private void CopyNonMarkdownFiles(
        string sourceDir,
        string destDir,
        HashSet<string>? generatedFiles,
        ImageCopyContext imageCopyContext,
        Collection<string> warnings)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetExtension(file).Equals(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(destDir);
            var outputPath = Path.Combine(destDir, Path.GetFileName(file));
            CopyOrOptimizeFile(file, outputPath, imageCopyContext, warnings);
            generatedFiles?.Add(Path.GetFullPath(outputPath));
        }
    }

    /// <summary>
    /// Bundles the state needed to decide whether a candidate file (Site static/ or Page Bundle
    /// asset) should be optimized as an image: the output directory (for web-path comparisons
    /// against the set of referenced web paths built by <see cref="IAssetReferenceIndexBuilder"/>),
    /// the project path (cache location + exclude-glob base), the effective
    /// <see cref="ImageOptions"/>, the set of referenced image web paths, and the rename
    /// manifest for extension changes (e.g. WebP conversion).
    /// </summary>
    private sealed record ImageCopyContext(
        string OutputDir,
        string ProjectPath,
        ImageOptions ImageOptions,
        HashSet<string> ReferencedImages,
        Dictionary<string, string> RenameManifest);

    private void CopyOrOptimizeFile(string sourceFile, string destPath, ImageCopyContext context, Collection<string> warnings)
    {
        var ext = Path.GetExtension(sourceFile);
        var webPath = AssetPipeline.ToWebPath(context.OutputDir, destPath);
        var shouldOptimize = context.ImageOptions.Enabled
            && imageOptimizer.CanOptimize(ext)
            && context.ReferencedImages.Contains(webPath)
            && !ImageExcludeMatcher.IsExcluded(sourceFile, context.ProjectPath, context.ImageOptions.Exclude);

        if (!shouldOptimize)
        {
            File.Copy(sourceFile, destPath, overwrite: true);
            return;
        }

        try
        {
            var settings = new ImageOptimizationSettings(context.ImageOptions.MaxWidth, context.ImageOptions.Quality, context.ImageOptions.Webp);
            var (bytes, resultExt) = ImageOptimizationCache.GetOrOptimize(context.ProjectPath, sourceFile, settings, imageOptimizer);
            var finalDestPath = resultExt.Equals(ext, StringComparison.OrdinalIgnoreCase)
                ? destPath
                : Path.ChangeExtension(destPath, resultExt);

            File.WriteAllBytes(finalDestPath, bytes);
            if (!string.Equals(finalDestPath, destPath, StringComparison.OrdinalIgnoreCase))
                context.RenameManifest[webPath] = AssetPipeline.ToWebPath(context.OutputDir, finalDestPath);
        }
#pragma warning disable CA1031 // Intentional: a broken source image should not abort the entire build
        catch (Exception ex)
#pragma warning restore CA1031
        {
            warnings.Add($"Image optimization failed for '{sourceFile}': {ex.Message} — using unoptimized copy.");
            File.Copy(sourceFile, destPath, overwrite: true);
        }
    }
}
