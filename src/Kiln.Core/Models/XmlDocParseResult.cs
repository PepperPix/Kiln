namespace Kiln.Models;

/// <summary>
/// The result of parsing an XML documentation file with <see cref="Services.XmlDocParser"/>.
/// </summary>
/// <param name="AssemblyName">The assembly name from the file, or <see langword="null"/> when it is missing.</param>
/// <param name="Members">The documented members.</param>
/// <param name="Warnings">Messages about entries that were skipped.</param>
public sealed record XmlDocParseResult(
    string? AssemblyName,
    IReadOnlyList<XmlDocMember> Members,
    IReadOnlyList<string> Warnings);
