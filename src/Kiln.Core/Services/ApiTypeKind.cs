namespace Kiln.Services;

/// <summary>The kind of a documented type as read from assembly metadata.</summary>
internal enum ApiTypeKind
{
    Class,
    StaticClass,
    Record,
    Struct,
    Interface,
    Enum,
    Delegate,
}
