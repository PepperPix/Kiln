namespace Kiln.Services;

using Kiln.Models;

public interface ITemplateRenderer
{
    string Render(ContentItem item, SiteConfiguration site, string themePath);

    string RenderCollectionIndex(
        ContentGroup collection,
        Paginator paginator,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        SiteConfiguration site,
        string themePath);

    string RenderTaxonomyTerm(
        TaxonomyTerm term,
        Paginator paginator,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        SiteConfiguration site,
        string themePath);

    string RenderTaxonomyOverview(
        TaxonomyDefinition taxonomy,
        IReadOnlyList<TaxonomyTerm> terms,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomies,
        SiteConfiguration site,
        string themePath);
}
