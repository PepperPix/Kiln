namespace Kiln.Models;

/// <summary>
/// A documented member (or type) read from a compiler-generated XML documentation file.
/// Text values are converted to Markdown.
/// </summary>
/// <param name="Kind">The kind of member.</param>
/// <param name="OwnerTypeFullName">The full name of the type that owns the member; for a type entry, the type itself.</param>
/// <param name="MemberSignature">The member name followed by its parameter list, if any; empty for a type entry.</param>
/// <param name="Summary">The summary text.</param>
/// <param name="Remarks">The remarks text.</param>
/// <param name="Params">The parameter descriptions as name and text pairs.</param>
/// <param name="Returns">The description of the return value.</param>
/// <param name="Exceptions">The documented exceptions as exception type and condition text pairs.</param>
public sealed record XmlDocMember(
    XmlDocMemberKind Kind,
    string OwnerTypeFullName,
    string MemberSignature,
    string Summary,
    string Remarks,
    IReadOnlyList<(string Name, string Text)> Params,
    string Returns,
    IReadOnlyList<(string Cref, string Text)> Exceptions);
