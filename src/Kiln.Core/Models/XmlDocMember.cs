namespace Kiln.Models;

public sealed record XmlDocMember(
    XmlDocMemberKind Kind,
    string OwnerTypeFullName,
    string MemberSignature,
    string Summary,
    string Remarks,
    IReadOnlyList<(string Name, string Text)> Params,
    string Returns,
    IReadOnlyList<(string Cref, string Text)> Exceptions);
