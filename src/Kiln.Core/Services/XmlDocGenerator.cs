namespace Kiln.Services;

using System.Text;
using Kiln.Models;

public sealed class XmlDocGenerator(IGeneratedContentWriter writer) : IXmlDocGenerator
{
    private readonly IGeneratedContentWriter _writer = writer;

    public DocGenReport Generate(string xmlDocPath, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(xmlDocPath);
        ArgumentNullException.ThrowIfNull(outputDir);

        XmlDocParseResult parseResult;
        try
        {
            parseResult = XmlDocParser.Parse(xmlDocPath);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (IOException ex)
        {
            return new DocGenReport([], [], [], [$"Failed to load XML doc file: {ex.Message}"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new DocGenReport([], [], [], [$"Failed to load XML doc file: {ex.Message}"]);
        }
        catch (System.Xml.XmlException ex)
        {
            return new DocGenReport([], [], [], [$"Failed to parse XML doc file: {ex.Message}"]);
        }

        var warnings = new List<string>(parseResult.Warnings);
        var written = new List<string>();
        var skipped = new List<string>();
        var conflicts = new List<string>();

        var typeGroups = parseResult.Members
            .GroupBy(m => m.OwnerTypeFullName, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        var weight = 0;
        foreach (var typeGroup in typeGroups)
        {
            weight++;

            var fullTypeName = typeGroup.Key;
            var members = typeGroup.ToList();
            var typeMember = members.Find(m => m.Kind == XmlDocMemberKind.Type);

            var (namespaceName, displayTypeName) = SplitTypeName(fullTypeName);

            var segments = fullTypeName.Split('.');
            var relativePath = segments.Length > 1
                ? string.Join(Path.AltDirectorySeparatorChar, segments[..^1]) + Path.AltDirectorySeparatorChar + segments[^1] + ".md"
                : segments[0] + ".md";

            var frontMatter = new List<(string Key, object Value)>
            {
                ("title", displayTypeName),
                ("weight", weight),
                ("generated", true),
                ("extra", new Dictionary<string, string>
                {
                    ["namespace"] = namespaceName,
                    ["assembly"] = parseResult.AssemblyName ?? "",
                }),
            };

            var body = BuildBody(displayTypeName, fullTypeName, typeMember, members);
            var file = new GeneratedContentFile(relativePath, frontMatter, body);

            var result = _writer.Write(outputDir, file);
            switch (result)
            {
                case WriteResult.Written:
                    written.Add(relativePath);
                    break;
                case WriteResult.SkippedAdopted:
                    skipped.Add(relativePath);
                    break;
                case WriteResult.Conflict:
                    conflicts.Add(relativePath);
                    break;
            }
        }

        return new DocGenReport(written, skipped, conflicts, warnings);
    }

    private static (string Namespace, string DisplayName) SplitTypeName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        var ns = lastDot >= 0 ? fullTypeName[..lastDot] : "";
        var shortName = lastDot >= 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;
        return (ns, ExpandGenericArity(shortName));
    }

    private static string BuildBody(
        string displayTypeName,
        string fullTypeName,
        XmlDocMember? typeMember,
        IReadOnlyList<XmlDocMember> members)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(displayTypeName).Append('\n');
        sb.Append('\n');
        sb.Append('`').Append(fullTypeName).Append("`\n");

        if (typeMember is not null)
        {
            if (!string.IsNullOrWhiteSpace(typeMember.Summary))
                sb.Append('\n').Append(typeMember.Summary).Append('\n');

            if (!string.IsNullOrWhiteSpace(typeMember.Remarks))
                sb.Append('\n').Append(typeMember.Remarks).Append('\n');
        }

        AppendSection(sb, "Properties", members, XmlDocMemberKind.Property, AppendPropertyOrField);
        AppendSection(sb, "Methods", members, XmlDocMemberKind.Method, AppendMethod);
        AppendSection(sb, "Fields", members, XmlDocMemberKind.Field, AppendPropertyOrField);
        AppendSection(sb, "Events", members, XmlDocMemberKind.Event, AppendPropertyOrField);

        return sb.ToString();
    }

    private static void AppendSection(
        StringBuilder sb,
        string sectionTitle,
        IReadOnlyList<XmlDocMember> members,
        XmlDocMemberKind kind,
        Action<StringBuilder, XmlDocMember> appendItem)
    {
        var sorted = members
            .Where(m => m.Kind == kind)
            .OrderBy(m => m.MemberSignature, StringComparer.Ordinal)
            .ToList();

        if (sorted.Count == 0)
            return;

        sb.Append('\n').Append("## ").Append(sectionTitle).Append('\n');

        foreach (var member in sorted)
            appendItem(sb, member);
    }

    private static void AppendPropertyOrField(StringBuilder sb, XmlDocMember member)
    {
        sb.Append('\n').Append("### ").Append(member.MemberSignature).Append('\n');

        if (!string.IsNullOrWhiteSpace(member.Summary))
            sb.Append('\n').Append(member.Summary).Append('\n');
    }

    private static void AppendMethod(StringBuilder sb, XmlDocMember member)
    {
        sb.Append('\n').Append("### ").Append(BuildReadableSignature(member.MemberSignature)).Append('\n');

        if (!string.IsNullOrWhiteSpace(member.Summary))
            sb.Append('\n').Append(member.Summary).Append('\n');

        if (member.Params.Count > 0)
        {
            sb.Append('\n').Append("**Parameters:**\n");
            foreach (var (name, text) in member.Params)
            {
                sb.Append("- `").Append(name).Append('`');
                if (!string.IsNullOrWhiteSpace(text))
                    sb.Append(" — ").Append(text);
                sb.Append('\n');
            }
        }

        if (!string.IsNullOrWhiteSpace(member.Returns))
            sb.Append('\n').Append("**Returns:** ").Append(member.Returns).Append('\n');

        if (member.Exceptions.Count > 0)
        {
            sb.Append('\n').Append("**Exceptions:**\n");
            foreach (var (cref, text) in member.Exceptions)
            {
                sb.Append("- `").Append(SimplifyType(cref)).Append('`');
                if (!string.IsNullOrWhiteSpace(text))
                    sb.Append(" — ").Append(text);
                sb.Append('\n');
            }
        }
    }

    private static string BuildReadableSignature(string memberSignature)
    {
        var parenIndex = memberSignature.IndexOf('(', StringComparison.Ordinal);
        var namePart = parenIndex >= 0 ? memberSignature[..parenIndex] : memberSignature;

        var paramsRaw = "";
        if (parenIndex >= 0 && memberSignature.EndsWith(')'))
            paramsRaw = memberSignature[(parenIndex + 1)..^1];

        var backtickIndex = namePart.IndexOf('`', StringComparison.Ordinal);
        var displayName = backtickIndex >= 0 ? namePart[..backtickIndex] : namePart;

        var paramTypes = string.IsNullOrEmpty(paramsRaw)
            ? []
            : SplitTopLevel(paramsRaw).Select(SimplifyType);

        return $"{displayName}({string.Join(", ", paramTypes)})";
    }

    private const int DoubleBacktickPrefixLength = 2;
    private const int SingleBacktickPrefixLength = 1;

    private static string SimplifyType(string type)
    {
        if (string.IsNullOrEmpty(type))
            return type;

        type = type.TrimEnd('@');

        if (type.StartsWith("``", StringComparison.Ordinal))
            return "T" + (type[DoubleBacktickPrefixLength..] == "0" ? "" : type[DoubleBacktickPrefixLength..]);

        if (type.StartsWith('`'))
            return "T" + (type[SingleBacktickPrefixLength..] == "0" ? "" : type[SingleBacktickPrefixLength..]);

        var braceIndex = type.IndexOf('{', StringComparison.Ordinal);
        if (braceIndex < 0)
        {
            var lastDot = type.LastIndexOf('.');
            return lastDot >= 0 ? type[(lastDot + 1)..] : type;
        }

        var baseName = type[..braceIndex];
        var lastDotBase = baseName.LastIndexOf('.');
        var simpleBase = lastDotBase >= 0 ? baseName[(lastDotBase + 1)..] : baseName;

        var innerEnd = type.LastIndexOf('}');
        var inner = innerEnd > braceIndex ? type[(braceIndex + 1)..innerEnd] : "";
        var innerArgs = SplitTopLevel(inner).Select(SimplifyType);

        return $"{simpleBase}<{string.Join(", ", innerArgs)}>";
    }

    private static List<string> SplitTopLevel(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(text[start..i]);
                    start = i + 1;
                    break;
            }
        }

        result.Add(text[start..]);
        return result;
    }

    private static string ExpandGenericArity(string nameWithArity)
    {
        var backtickIndex = nameWithArity.IndexOf('`', StringComparison.Ordinal);
        if (backtickIndex < 0)
            return nameWithArity;

        var baseName = nameWithArity[..backtickIndex];
        var aritySpan = nameWithArity[(backtickIndex + 1)..];

        if (!int.TryParse(aritySpan, out var arity) || arity <= 0)
            return baseName;

        var typeParams = string.Join(", ", Enumerable.Range(1, arity).Select(n => arity == 1 ? "T" : $"T{n}"));
        return $"{baseName}<{typeParams}>";
    }
}
