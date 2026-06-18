namespace Kiln.Models;

using System.Collections.ObjectModel;

public sealed class ContentGroup
{
    public required string Name { get; init; }
    public string Directory { get; init; } = "";
    public string Permalink { get; init; } = "/:slug/";
    public string Sort { get; init; } = "none";
    public bool Feed { get; init; }
    public int? Paginate { get; init; }
    public string Layout { get; init; } = "default";
    public Collection<string> Taxonomies { get; init; } = [];
    public Dictionary<string, string> References { get; init; } = [];
    public Dictionary<string, object> Plugins { get; init; } = [];
    public Dictionary<string, object> Extra { get; init; } = [];

    // Populated during build
    public Collection<ContentItem> Items { get; } = [];
    public Uri Url
    {
        get
        {
            var sep = Path.AltDirectorySeparatorChar;
            return new Uri($"{sep}{Name}{sep}", UriKind.Relative);
        }
    }
}
