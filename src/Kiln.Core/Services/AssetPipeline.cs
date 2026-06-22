namespace Kiln.Services;

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Kiln.Abstractions;
using Kiln.Models;

internal sealed partial class AssetPipeline
{
    private const int HashLength = 8;

    private static readonly string[] FingerprintExtensions =
        [".css", ".js", ".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".woff", ".woff2", ".ttf", ".otf"];

    public static async Task RunAsync(
        string outputDir,
        string assetPrefix,
        BuildOptions buildOptions,
        IAssetMinifier minifier,
        Collection<string> warnings,
        Collection<string> errors,
        CancellationToken ct)
    {
        // Stage A1: Minify CSS/JS/SVG assets (before fingerprinting, so hashes cover minified bytes)
        await RunMinifyAssetsAsync(outputDir, assetPrefix, buildOptions, minifier, warnings, ct).ConfigureAwait(false);

        // Stage B: Fingerprint — hash minified assets and rewrite references while the HTML still
        // has its original (quoted) attribute formatting.
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (buildOptions.Fingerprint)
            RunFingerprint(outputDir, assetPrefix, manifest, warnings);

        // Stage A2: Minify HTML last, so attribute-quote removal can't break the reference rewrite.
        if (buildOptions.MinifyHtml && minifier.CanMinify(AssetType.Html))
            await RunMinifyHtmlAsync(outputDir, buildOptions, minifier, warnings, ct).ConfigureAwait(false);

        // Stage C: Link-check (AngleSharp parses the final HTML, robust to quoting)
        if (buildOptions.LinkCheck)
            await RunLinkCheckAsync(outputDir, errors, ct).ConfigureAwait(false);
    }

    // ── Stage A: Minify ───────────────────────────────────────────────────

    private static async Task RunMinifyAssetsAsync(
        string outputDir,
        string assetPrefix,
        BuildOptions buildOptions,
        IAssetMinifier minifier,
        Collection<string> warnings,
        CancellationToken ct)
    {
        var assetsDir = Path.Combine(outputDir, assetPrefix.Trim('/'));
        if (!Directory.Exists(assetsDir)) return;

        foreach (var file in Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file);
            if (ext.Equals(".css", StringComparison.OrdinalIgnoreCase) && buildOptions.MinifyCss && minifier.CanMinify(AssetType.Css))
                await MinifyFileAsync(file, AssetType.Css, minifier, warnings, ct).ConfigureAwait(false);
            else if (ext.Equals(".js", StringComparison.OrdinalIgnoreCase) && buildOptions.MinifyJs && minifier.CanMinify(AssetType.Js))
                await MinifyFileAsync(file, AssetType.Js, minifier, warnings, ct).ConfigureAwait(false);
            else if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) && buildOptions.MinifySvg && minifier.CanMinify(AssetType.Svg))
                await MinifyFileAsync(file, AssetType.Svg, minifier, warnings, ct).ConfigureAwait(false);
        }
    }

    private static async Task RunMinifyHtmlAsync(
        string outputDir,
        BuildOptions buildOptions,
        IAssetMinifier minifier,
        Collection<string> warnings,
        CancellationToken ct)
    {
        // Use aggressive variant if requested and supported.
        var htmlMinifier = buildOptions.HtmlAggressive && minifier is NuglifyAssetMinifier
            ? new NuglifyAssetMinifier(htmlAggressive: true)
            : minifier;

        foreach (var file in Directory.EnumerateFiles(outputDir, "*.html", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            await MinifyFileAsync(file, AssetType.Html, htmlMinifier, warnings, ct).ConfigureAwait(false);
        }
    }

    private static async Task MinifyFileAsync(
        string filePath,
        AssetType assetType,
        IAssetMinifier minifier,
        Collection<string> warnings,
        CancellationToken ct)
    {
        var original = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        string minified;
        try
        {
            minified = minifier.Minify(original, assetType);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            warnings.Add($"Minification failed for '{filePath}': {ex.Message} — using original.");
            return;
        }

        if (!string.IsNullOrEmpty(minified) && minified != original)
            await File.WriteAllTextAsync(filePath, minified, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    // ── Stage B: Fingerprint ──────────────────────────────────────────────

    private static void RunFingerprint(
        string outputDir,
        string assetPrefix,
        Dictionary<string, string> manifest,
        Collection<string> warnings)
    {
        var assetDir = Path.Combine(outputDir, assetPrefix.Trim('/'));
        if (!Directory.Exists(assetDir)) return;

        // Build manifest: originalUrl → hashedUrl
        foreach (var file in Directory.EnumerateFiles(assetDir, "*", SearchOption.AllDirectories)
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase))
        {
            var ext = Path.GetExtension(file);
            if (!FingerprintExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

            var bytes = File.ReadAllBytes(file);
            var hash = ComputeHash8(bytes);

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
            var hashedName = $"{fileNameWithoutExt}.{hash}{ext}";
            var hashedPath = Path.Combine(Path.GetDirectoryName(file)!, hashedName);

            if (!File.Exists(hashedPath))
                File.Move(file, hashedPath);
            else
            {
                warnings.Add($"Fingerprint: hashed file already exists '{hashedPath}', skipping rename.");
                continue;
            }

            // Build URL keys: relative to outputDir, forward slashes, leading slash
            var originalUrl = ToWebPath(outputDir, file);
            var hashedUrl = ToWebPath(outputDir, hashedPath);
            manifest[originalUrl] = hashedUrl;
        }

        // Rewrite references in HTML files
        foreach (var htmlFile in Directory.EnumerateFiles(outputDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase))
        {
            RewriteHtmlReferences(htmlFile, manifest);
        }

        // Rewrite references in CSS files
        foreach (var cssFile in Directory.EnumerateFiles(assetDir, "*.css", SearchOption.AllDirectories)
            .OrderBy(static f => f, StringComparer.OrdinalIgnoreCase))
        {
            RewriteCssReferences(cssFile, manifest);
        }
    }

    private static string ComputeHash8(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..HashLength];
    }

    private static string ToWebPath(string outputDir, string fullPath)
    {
#pragma warning disable S1075 // URL paths use '/' by spec, independent of the OS path separator
        return "/" + Path.GetRelativePath(outputDir, fullPath).Replace(Path.DirectorySeparatorChar, '/');
#pragma warning restore S1075
    }

    private static void RewriteHtmlReferences(string htmlFile, Dictionary<string, string> manifest)
    {
        if (manifest.Count == 0) return;

        var content = File.ReadAllText(htmlFile);
        var changed = false;

        foreach (var (original, hashed) in manifest)
        {
            // Match href="..." or src="..." containing the original URL
            var hrefPattern = $"href=\"{EscapeForAttributeMatch(original)}\"";
            var srcPattern = $"src=\"{EscapeForAttributeMatch(original)}\"";
            var newHref = $"href=\"{hashed}\"";
            var newSrc = $"src=\"{hashed}\"";

            if (content.Contains(hrefPattern, StringComparison.OrdinalIgnoreCase))
            {
                content = content.Replace(hrefPattern, newHref, StringComparison.OrdinalIgnoreCase);
                changed = true;
            }
            if (content.Contains(srcPattern, StringComparison.OrdinalIgnoreCase))
            {
                content = content.Replace(srcPattern, newSrc, StringComparison.OrdinalIgnoreCase);
                changed = true;
            }
        }

        if (changed)
            File.WriteAllText(htmlFile, content, Encoding.UTF8);
    }

    private static void RewriteCssReferences(string cssFile, Dictionary<string, string> manifest)
    {
        if (manifest.Count == 0) return;

        var content = File.ReadAllText(cssFile);
        var changed = false;

        foreach (var (original, hashed) in manifest)
        {
            // Match url("..."), url('...'), url(...)
            var patterns = new[]
            {
                $"url(\"{EscapeForAttributeMatch(original)}\")",
                $"url('{EscapeForAttributeMatch(original)}')",
                $"url({EscapeForAttributeMatch(original)})",
            };
            var replacements = new[]
            {
                $"url(\"{hashed}\")",
                $"url('{hashed}')",
                $"url({hashed})",
            };

            for (var i = 0; i < patterns.Length; i++)
            {
                if (content.Contains(patterns[i], StringComparison.OrdinalIgnoreCase))
                {
                    content = content.Replace(patterns[i], replacements[i], StringComparison.OrdinalIgnoreCase);
                    changed = true;
                }
            }
        }

        if (changed)
            File.WriteAllText(cssFile, content, Encoding.UTF8);
    }

    private static string EscapeForAttributeMatch(string url) => url;

    // ── Stage C: Link-check ───────────────────────────────────────────────

    private static async Task RunLinkCheckAsync(
        string outputDir,
        Collection<string> errors,
        CancellationToken ct)
    {
        // Build set of all output files as URL paths
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories))
            knownPaths.Add(ToWebPath(outputDir, file));

        var deadLinks = new List<(string SourceFile, string DeadRef)>();

        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var parser = context.GetService<IHtmlParser>()!;

        // Check HTML files
        foreach (var htmlFile in Directory.EnumerateFiles(outputDir, "*.html", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(htmlFile, ct).ConfigureAwait(false);
            var document = await parser.ParseDocumentAsync(content, ct).ConfigureAwait(false);

            var hrefRefs = document.QuerySelectorAll("[href]")
                .Select(static el => el.GetAttribute("href"))
                .Where(static h => h is not null)
                .Cast<string>();

            var srcRefs = document.QuerySelectorAll("[src]")
                .Select(static el => el.GetAttribute("src"))
                .Where(static s => s is not null)
                .Cast<string>();

            foreach (var rawRef in hrefRefs.Concat(srcRefs))
            {
                if (!IsInternalRef(rawRef)) continue;
                var resolved = ResolveInternalRef(rawRef, knownPaths);
                if (resolved is null)
                {
                    var rel = Path.GetRelativePath(outputDir, htmlFile);
                    deadLinks.Add((rel, rawRef));
                }
            }
        }

        // Check CSS url() references
        foreach (var cssFile in Directory.EnumerateFiles(outputDir, "*.css", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(cssFile, ct).ConfigureAwait(false);
            foreach (var rawRef in ExtractCssUrls(content))
            {
                if (!IsInternalRef(rawRef)) continue;
                if (!knownPaths.Contains(rawRef))
                {
                    var rel = Path.GetRelativePath(outputDir, cssFile);
                    deadLinks.Add((rel, rawRef));
                }
            }
        }

        foreach (var (source, dead) in deadLinks)
            errors.Add($"Dead link in '{source}': '{dead}'");
    }

    private static bool IsInternalRef(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return false;
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        if (url.StartsWith("//", StringComparison.Ordinal)) return false;
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
        if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;
        if (url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) return false;
        if (url.StartsWith("#", StringComparison.Ordinal)) return false;
        if (!url.StartsWith("/", StringComparison.Ordinal)) return false;
        return true;
    }

    private static string? ResolveInternalRef(string url, HashSet<string> knownPaths)
    {
        // Strip fragment
        var hashPos = url.AsSpan().IndexOf('#');
        var urlWithoutFragment = hashPos >= 0 ? url[..hashPos] : url;
        if (string.IsNullOrEmpty(urlWithoutFragment)) return urlWithoutFragment; // anchor-only

        // Direct match
        if (knownPaths.Contains(urlWithoutFragment)) return urlWithoutFragment;

        // Try index.html for directory-style URLs
        var withIndex = urlWithoutFragment.TrimEnd('/') + "/index.html";
        if (knownPaths.Contains(withIndex)) return withIndex;

        // Try appending /index.html (for URLs without trailing slash and no extension)
        if (!Path.HasExtension(urlWithoutFragment))
        {
            var withSlashIndex = urlWithoutFragment + "/index.html";
            if (knownPaths.Contains(withSlashIndex)) return withSlashIndex;
        }

        return null;
    }

    private static IEnumerable<string> ExtractCssUrls(string cssContent)
    {
        var matches = CssUrlRegex().Matches(cssContent);
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            var url = m.Groups[1].Value.Trim('\'', '"').Trim();
            if (!string.IsNullOrWhiteSpace(url))
                yield return url;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"url\(\s*([^)]+)\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex CssUrlRegex();
}
