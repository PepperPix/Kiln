namespace Kiln.Services;

using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Metadata read from the <c>.nuspec</c> inside a plugin package.
/// </summary>
internal sealed record PluginNuspecInfo(
    IReadOnlyList<string> Tags,
    string? Description,
    IReadOnlyList<string> Authors,
    string? License,
    Uri? ProjectUrl)
{
    private const long MaxNuspecBytes = 1024 * 1024;
    private static readonly char[] ListSeparators = [' ', ',', ';'];

    /// <summary>
    /// Reads the root-level nuspec of <paramref name="archive"/>; returns <c>null</c> if it is missing, oversized or not valid XML.
    /// </summary>
    public static PluginNuspecInfo? Read(ZipArchive archive)
    {
        var nuspec = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/', StringComparison.Ordinal));
        if (nuspec is null || nuspec.Length > MaxNuspecBytes)
            return null;

        try
        {
            using var stream = nuspec.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = MaxNuspecBytes });
            var elements = XDocument.Load(reader).Descendants().ToList();

            string? Value(string localName)
            {
                var text = elements.Find(e => e.Name.LocalName == localName)?.Value.Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }

            var license = elements.Find(e => e.Name.LocalName == "license");
            var licenseText = license is null || string.IsNullOrWhiteSpace(license.Value)
                ? Value("licenseUrl")
                : license.Value.Trim();

            var projectUrl = Value("projectUrl");

            return new PluginNuspecInfo(
                Split(Value("tags"), ListSeparators),
                Value("description"),
                Split(Value("authors"), [',', ';']),
                licenseText,
                Uri.TryCreate(projectUrl, UriKind.Absolute, out var uri) ? uri : null);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns whether the package carries the given tag (case-insensitive).
    /// </summary>
    public bool HasTag(string tag)
        => Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));

    private static string[] Split(string? value, char[] separators)
        => value is null
            ? []
            : value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
