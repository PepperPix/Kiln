namespace Kiln.Services;

using System.Globalization;
using System.Text;
using System.Xml;
using Kiln.Models;

internal static class SitemapGenerator
{
    public static string Generate(
        SiteConfiguration config,
        IReadOnlyList<ContentItem> allItems,
        IReadOnlyDictionary<string, IReadOnlyList<TaxonomyTerm>> allTaxonomyTerms,
        bool includeDrafts)
    {
        var baseUrl = config.BaseUrl.ToString().TrimEnd('/');
        var buildDate = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using var sw = new Utf8StringWriter();
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  " };
        using var writer = XmlWriter.Create(sw, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

        // Content items
        foreach (var item in allItems)
        {
            if (item.Draft && !includeDrafts) continue;
            var lastmod = item.Date.HasValue
                ? item.Date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : buildDate;
            WriteUrl(writer, $"{baseUrl}{item.Url.OriginalString}", lastmod);
        }

        // Collection index pages (paginated)
        foreach (var collection in config.Collections.Values)
        {
            if (!(collection.Paginate > 0)) continue;
            var nonDraftCount = allItems.Count(
                i => i.Collection.Name == collection.Name && (!i.Draft || includeDrafts));
            if (nonDraftCount == 0) continue;

            var totalPages = (int)Math.Ceiling(nonDraftCount / (double)collection.Paginate!.Value);
            var indexUrl = collection.IndexUrl.OriginalString;

            WriteUrl(writer, $"{baseUrl}{indexUrl}", buildDate);
            for (var p = 2; p <= totalPages; p++)
                WriteUrl(writer, $"{baseUrl}{indexUrl.TrimEnd('/')}/page/{p}/", buildDate);
        }

        // Taxonomy overview + term pages
        foreach (var (taxName, terms) in allTaxonomyTerms)
        {
            if (!config.Taxonomies.TryGetValue(taxName, out var taxDef)) continue;

            var overviewUrl = TemplateRenderer.GetTaxonomyOverviewUrl(taxDef).OriginalString;
            WriteUrl(writer, $"{baseUrl}{overviewUrl}", buildDate);

            foreach (var term in terms)
            {
                WriteUrl(writer, $"{baseUrl}{term.Url.OriginalString}", buildDate);
                if (taxDef.Paginate > 0)
                {
                    var totalPages = (int)Math.Ceiling(term.Count / (double)taxDef.Paginate!.Value);
                    for (var p = 2; p <= totalPages; p++)
                        WriteUrl(writer, $"{baseUrl}{term.Url.OriginalString.TrimEnd('/')}/page/{p}/", buildDate);
                }
            }
        }

        writer.WriteEndElement(); // urlset
        writer.WriteEndDocument();
        writer.Flush();
        return sw.ToString();
    }

    private static void WriteUrl(XmlWriter writer, string loc, string lastmod)
    {
        writer.WriteStartElement("url");
        writer.WriteElementString("loc", loc);
        writer.WriteElementString("lastmod", lastmod);
        writer.WriteEndElement();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
