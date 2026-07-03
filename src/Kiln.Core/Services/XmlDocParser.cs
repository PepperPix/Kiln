namespace Kiln.Services;

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Kiln.Models;

public static partial class XmlDocParser
{
    private const int MemberNamePrefixLength = 2;

    public static XmlDocParseResult Parse(string xmlPath)
    {
        ArgumentNullException.ThrowIfNull(xmlPath);

        var doc = XDocument.Load(xmlPath);
        var assemblyName = doc.Root?.Element("assembly")?.Element("name")?.Value.Trim();

        var members = new List<XmlDocMember>();
        var warnings = new List<string>();

        var membersElement = doc.Root?.Element("members");
        if (membersElement is not null)
        {
            foreach (var memberElement in membersElement.Elements("member"))
                ParseMember(memberElement, members, warnings);
        }

        return new XmlDocParseResult(
            string.IsNullOrWhiteSpace(assemblyName) ? null : assemblyName,
            members,
            warnings);
    }

    private static void ParseMember(XElement memberElement, List<XmlDocMember> members, List<string> warnings)
    {
        var name = memberElement.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(name) || name.Length < MemberNamePrefixLength || name[1] != ':')
        {
            warnings.Add($"Skipped member with invalid or missing name: '{name}'.");
            return;
        }

        var kind = name[0] switch
        {
            'T' => XmlDocMemberKind.Type,
            'M' => XmlDocMemberKind.Method,
            'P' => XmlDocMemberKind.Property,
            'F' => XmlDocMemberKind.Field,
            'E' => XmlDocMemberKind.Event,
            _ => (XmlDocMemberKind?)null,
        };

        if (kind is null)
        {
            warnings.Add($"Skipped member with unknown kind prefix: '{name}'.");
            return;
        }

        var (ownerTypeFullName, memberSignature) = SplitOwnerAndMember(kind.Value, name[MemberNamePrefixLength..]);
        if (string.IsNullOrWhiteSpace(ownerTypeFullName))
        {
            warnings.Add($"Skipped member with no resolvable owner type: '{name}'.");
            return;
        }

        var summary = ExtractMarkdown(memberElement.Element("summary"));
        var remarks = ExtractMarkdown(memberElement.Element("remarks"));
        var returns = ExtractMarkdown(memberElement.Element("returns"));

        var paramList = memberElement.Elements("param")
            .Select(p => (p.Attribute("name")?.Value ?? "", ExtractMarkdown(p)))
            .ToList();

        var exceptionList = memberElement.Elements("exception")
            .Select(e => (StripCrefPrefix(e.Attribute("cref")?.Value ?? ""), ExtractMarkdown(e)))
            .ToList();

        members.Add(new XmlDocMember(
            kind.Value,
            ownerTypeFullName,
            memberSignature,
            summary,
            remarks,
            paramList,
            returns,
            exceptionList));
    }

    private static (string OwnerTypeFullName, string MemberSignature) SplitOwnerAndMember(
        XmlDocMemberKind kind,
        string rest)
    {
        if (kind == XmlDocMemberKind.Type)
            return (rest, "");

        var parenIndex = rest.IndexOf('(', StringComparison.Ordinal);
        var namePart = parenIndex >= 0 ? rest[..parenIndex] : rest;
        var paramPart = parenIndex >= 0 ? rest[parenIndex..] : "";

        var lastDot = namePart.LastIndexOf('.');
        if (lastDot < 0)
            return ("", namePart + paramPart);

        var owner = namePart[..lastDot];
        var memberName = namePart[(lastDot + 1)..];
        return (owner, memberName + paramPart);
    }

    internal static string StripCrefPrefix(string cref)
    {
        if (cref.Length > MemberNamePrefixLength && cref[1] == ':')
            return cref[MemberNamePrefixLength..];

        return cref;
    }

    internal static string ExtractMarkdown(XElement? element)
    {
        if (element is null)
            return "";

        var sb = new StringBuilder();
        AppendNodes(element.Nodes(), sb);
        return CollapseBlankLines(sb.ToString());
    }

    private static void AppendNodes(IEnumerable<XNode> nodes, StringBuilder sb)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case XText text:
                    sb.Append(NormalizeInlineWhitespace(text.Value));
                    break;
                case XElement element:
                    AppendElement(element, sb);
                    break;
            }
        }
    }

    private static void AppendElement(XElement element, StringBuilder sb)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            {
                var cref = element.Attribute("cref")?.Value;
                var text = !string.IsNullOrWhiteSpace(cref) ? StripCrefPrefix(cref) : element.Value.Trim();
                sb.Append('`').Append(text).Append('`');
                break;
            }
            case "paramref":
            case "typeparamref":
            {
                var refName = element.Attribute("name")?.Value ?? "";
                sb.Append('`').Append(refName).Append('`');
                break;
            }
            case "c":
                sb.Append('`').Append(element.Value.Trim()).Append('`');
                break;
            case "code":
                sb.Append("\n\n```\n").Append(element.Value.Trim('\n', '\r')).Append("\n```\n\n");
                break;
            case "para":
                sb.Append("\n\n");
                AppendNodes(element.Nodes(), sb);
                sb.Append("\n\n");
                break;
            default:
                AppendNodes(element.Nodes(), sb);
                break;
        }
    }

    private static string NormalizeInlineWhitespace(string text) => WhitespaceRunPattern().Replace(text, " ");

    private static string CollapseBlankLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return BlankLineRunPattern().Replace(normalized, "\n\n");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunPattern();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLineRunPattern();
}
