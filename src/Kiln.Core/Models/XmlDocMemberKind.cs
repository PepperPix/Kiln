namespace Kiln.Models;

/// <summary>
/// The kind of member described by an XML documentation entry.
/// </summary>
public enum XmlDocMemberKind
{
    /// <summary>A type.</summary>
    Type,

    /// <summary>A method.</summary>
    Method,

    /// <summary>A property.</summary>
    Property,

    /// <summary>A field.</summary>
    Field,

    /// <summary>An event.</summary>
    Event,
}
