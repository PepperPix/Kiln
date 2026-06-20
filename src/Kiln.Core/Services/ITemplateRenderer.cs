namespace Kiln.Services;

using Kiln.Models;

public interface ITemplateRenderer
{
    string Render(ContentItem item, SharedRenderContext sharedContext, SiteConfiguration site, string themePath, IReadOnlyList<PluginDefinition> plugins);

    string RenderCollectionIndex(
        ContentGroup collection,
        Paginator paginator,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins);

    string RenderTaxonomyTerm(
        TaxonomyTerm term,
        Paginator paginator,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins);

    string RenderTaxonomyOverview(
        TaxonomyDefinition taxonomy,
        IReadOnlyList<TaxonomyTerm> terms,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins);

    string RenderNotFound(SharedRenderContext sharedContext, SiteConfiguration site, string themePath, IReadOnlyList<PluginDefinition> plugins);
}
