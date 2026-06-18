namespace Kiln.Services;

using System.Globalization;
using System.Text;
using Kiln.Models;

public sealed class PermalinkGenerator : IPermalinkGenerator
{
    public Uri Generate(ContentItem item, ContentGroup collection)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(collection);

        // Frontmatter url: overrides collection pattern
        if (item.Extra.TryGetValue("permalink_override", out var overrideObj)
            && overrideObj is string urlOverride
            && !string.IsNullOrEmpty(urlOverride))
        {
            return BuildRelativeUri(urlOverride);
        }

        var pattern = collection.Permalink;

        var result = pattern
            .Replace(":slug", item.Slug, StringComparison.Ordinal)
            .Replace(":collection", collection.Name, StringComparison.Ordinal);

        if (item.Date.HasValue)
        {
            result = result
                .Replace(":year", item.Date.Value.Year.ToString("D4", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace(":month", item.Date.Value.Month.ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace(":day", item.Date.Value.Day.ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        return BuildRelativeUri(result);
    }

    private static Uri BuildRelativeUri(string path)
    {
        var sep = Path.AltDirectorySeparatorChar;
        var segments = path.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        sb.Append(sep);
        sb.AppendJoin(sep, segments);
        sb.Append(sep);
        return new Uri(sb.ToString(), UriKind.Relative);
    }
}
