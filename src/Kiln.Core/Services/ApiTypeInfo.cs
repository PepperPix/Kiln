namespace Kiln.Services;

/// <summary>A publicly visible type with the names of its externally visible members.</summary>
/// <param name="FullName">The type name in XML documentation format (namespace, dot-separated nesting, backtick arity).</param>
/// <param name="Kind">The kind of the type.</param>
/// <param name="MemberNames">Names of the visible members; constructors use the XML names <c>#ctor</c>/<c>#cctor</c>.</param>
internal sealed record ApiTypeInfo(string FullName, ApiTypeKind Kind, IReadOnlySet<string> MemberNames);
