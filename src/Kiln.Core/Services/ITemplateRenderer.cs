namespace Kiln.Services;

using Kiln.Models;

/// <summary>
/// Renders pages with the layouts of a theme.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders a content item with the layout named by the item, or by its collection when the item names none.
    /// The layout <c>default.html</c> is used as a fallback.
    /// </summary>
    /// <param name="item">The item to render.</param>
    /// <param name="sharedContext">The render data shared by all pages of a build.</param>
    /// <param name="site">The site configuration.</param>
    /// <param name="themePath">The theme directory containing the <c>layouts</c> directory.</param>
    /// <param name="plugins">The plugins of the site.</param>
    /// <returns>The rendered HTML.</returns>
    /// <exception cref="FileNotFoundException">The theme contains none of the candidate layouts.</exception>
    string Render(ContentItem item, SharedRenderContext sharedContext, SiteConfiguration site, string themePath, IReadOnlyList<PluginDefinition> plugins);

    /// <summary>
    /// Renders one page of a collection index with the layout <c>{collection}-index.html</c>,
    /// falling back to <c>index.html</c> and <c>default.html</c>.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="paginator">The page to render.</param>
    /// <param name="sharedContext">The render data shared by all pages of a build.</param>
    /// <param name="site">The site configuration.</param>
    /// <param name="themePath">The theme directory containing the <c>layouts</c> directory.</param>
    /// <param name="plugins">The plugins of the site.</param>
    /// <returns>The rendered HTML.</returns>
    /// <exception cref="FileNotFoundException">The theme contains none of the candidate layouts.</exception>
    string RenderCollectionIndex(
        ContentGroup collection,
        Paginator paginator,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins);

    /// <summary>
    /// Renders one page of a taxonomy term with the layout <c>taxonomy-{taxonomy}.html</c>,
    /// falling back to <c>taxonomy.html</c> and <c>default.html</c>.
    /// </summary>
    /// <param name="term">The taxonomy term.</param>
    /// <param name="paginator">The page to render.</param>
    /// <param name="sharedContext">The render data shared by all pages of a build.</param>
    /// <param name="site">The site configuration.</param>
    /// <param name="themePath">The theme directory containing the <c>layouts</c> directory.</param>
    /// <param name="plugins">The plugins of the site.</param>
    /// <returns>The rendered HTML.</returns>
    /// <exception cref="FileNotFoundException">The theme contains none of the candidate layouts.</exception>
    string RenderTaxonomyTerm(
        TaxonomyTerm term,
        Paginator paginator,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins);

    /// <summary>
    /// Renders the overview page of a taxonomy with the layout <c>taxonomy-{taxonomy}-index.html</c>,
    /// falling back to <c>taxonomy-index.html</c> and <c>default.html</c>.
    /// </summary>
    /// <param name="taxonomy">The taxonomy.</param>
    /// <param name="terms">The terms of the taxonomy.</param>
    /// <param name="sharedContext">The render data shared by all pages of a build.</param>
    /// <param name="site">The site configuration.</param>
    /// <param name="themePath">The theme directory containing the <c>layouts</c> directory.</param>
    /// <param name="plugins">The plugins of the site.</param>
    /// <returns>The rendered HTML.</returns>
    /// <exception cref="FileNotFoundException">The theme contains none of the candidate layouts.</exception>
    string RenderTaxonomyOverview(
        TaxonomyDefinition taxonomy,
        IReadOnlyList<TaxonomyTerm> terms,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins);

    /// <summary>
    /// Renders the not-found page with the layout <c>404.html</c>.
    /// </summary>
    /// <param name="sharedContext">The render data shared by all pages of a build.</param>
    /// <param name="site">The site configuration.</param>
    /// <param name="themePath">The theme directory containing the <c>layouts</c> directory.</param>
    /// <param name="plugins">The plugins of the site.</param>
    /// <returns>The rendered HTML.</returns>
    /// <exception cref="FileNotFoundException">The theme has no <c>404.html</c> layout.</exception>
    string RenderNotFound(SharedRenderContext sharedContext, SiteConfiguration site, string themePath, IReadOnlyList<PluginDefinition> plugins);
}
