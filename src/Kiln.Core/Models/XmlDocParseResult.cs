namespace Kiln.Models;

public sealed record XmlDocParseResult(
    string? AssemblyName,
    IReadOnlyList<XmlDocMember> Members,
    IReadOnlyList<string> Warnings);
