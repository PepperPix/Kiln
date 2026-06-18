namespace Kiln.Services;

using System.Globalization;
using System.Text;
using System.Xml;
using Kiln.Models;

internal static class FeedGenerator
{
    private const int FeedLimit = 20;

    public static string GenerateAtom(
        ContentGroup collection,
        IReadOnlyList<ContentItem> items,
        SiteConfiguration config)
    {
        var baseUrl = config.BaseUrl.ToString().TrimEnd('/');
        var indexUrl = collection.IndexUrl.OriginalString;

        var feedItems = items
            .Where(static i => !i.Draft)
            .OrderByDescending(i => i.Date ?? DateTime.MinValue)
            .Take(FeedLimit)
            .ToList();

        using var sw = new Utf8StringWriter();
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  " };
        using var writer = XmlWriter.Create(sw, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("feed", "http://www.w3.org/2005/Atom");

        writer.WriteElementString("title", $"{config.Title} \u2014 {collection.Name}");

        writer.WriteStartElement("link");
        writer.WriteAttributeString("href", $"{baseUrl}{indexUrl}");
        writer.WriteAttributeString("rel", "alternate");
        writer.WriteEndElement();

        writer.WriteStartElement("link");
        writer.WriteAttributeString("href", $"{baseUrl}{indexUrl}feed.xml");
        writer.WriteAttributeString("rel", "self");
        writer.WriteEndElement();

        writer.WriteElementString("id", $"{baseUrl}{indexUrl}");

        var updated = feedItems.Count > 0 && feedItems[0].Date.HasValue
            ? feedItems[0].Date!.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        writer.WriteElementString("updated", updated);

        foreach (var item in feedItems)
        {
            writer.WriteStartElement("entry");

            writer.WriteElementString("title", item.Title);

            writer.WriteStartElement("link");
            writer.WriteAttributeString("href", $"{baseUrl}{item.Url.OriginalString}");
            writer.WriteEndElement();

            writer.WriteElementString("id", $"{baseUrl}{item.Url.OriginalString}");

            if (item.Date.HasValue)
                writer.WriteElementString("updated",
                    item.Date.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

            if (!string.IsNullOrEmpty(item.Description))
                writer.WriteElementString("summary", item.Description);

            if (!string.IsNullOrEmpty(item.HtmlContent))
            {
                writer.WriteStartElement("content");
                writer.WriteAttributeString("type", "html");
                writer.WriteString(item.HtmlContent);
                writer.WriteEndElement();
            }

            writer.WriteEndElement(); // entry
        }

        writer.WriteEndElement(); // feed
        writer.WriteEndDocument();
        writer.Flush();
        return sw.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
