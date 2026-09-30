namespace Kiln.Services;

/// <summary>A publicly visible type with the names of its externally visible members.</summary>
internal sealed class ApiTypeInfo
{
    /// <summary>Gets the kind of the type.</summary>
    public required ApiTypeKind Kind { get; init; }

    /// <summary>Gets the names of the visible members; constructors use the XML names <c>#ctor</c>/<c>#cctor</c>.</summary>
    public required IReadOnlySet<string> MemberNames { get; init; }
}
