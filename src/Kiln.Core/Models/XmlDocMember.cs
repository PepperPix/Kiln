namespace Kiln.Models;

public enum XmlDocMemberKind
{
    Type,
    Method,
    Property,
    Field,
    Event,
}

public sealed record XmlDocMember(
    XmlDocMemberKind Kind,
    string OwnerTypeFullName,
    string MemberSignature,
    string Summary,
    string Remarks,
    IReadOnlyList<(string Name, string Text)> Params,
    string Returns,
    IReadOnlyList<(string Cref, string Text)> Exceptions);

public sealed record XmlDocParseResult(
    string? AssemblyName,
    IReadOnlyList<XmlDocMember> Members,
    IReadOnlyList<string> Warnings);
