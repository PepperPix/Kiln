namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Bundles the render-time state shared by all page-rendering phases (content items,
/// collection indexes, taxonomy pages, 404, asset copying) — keeps phase method parameter
/// counts within the S107 limit without changing behavior.
/// </summary>
internal sealed record RenderPassContext(
    SharedRenderContext SharedContext,
    SiteConfiguration Config,
    string ThemePath,
    IReadOnlyList<PluginDefinition> Plugins,
    string OutputDir,
    HashSet<string>? GeneratedFiles);
