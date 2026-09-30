namespace Kiln.Services;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

/// <summary>
/// The publicly visible API of a compiled assembly, read from its metadata without loading it.
/// </summary>
/// <remarks>
/// Members are matched by name only (no parameter types): an <c>internal</c> overload that shares its name with a visible member is not told apart.
/// </remarks>
internal sealed class AssemblyApiSurface
{
    private readonly Dictionary<string, ApiTypeInfo> _types;

    private AssemblyApiSurface(Dictionary<string, ApiTypeInfo> types)
    {
        _types = types;
    }

    /// <summary>Returns the visible type with the given XML documentation name, or <see langword="null"/> when it is not publicly visible.</summary>
    public ApiTypeInfo? Find(string fullName) => _types.GetValueOrDefault(fullName);

    /// <summary>Reads the visible API surface of the assembly at <paramref name="assemblyPath"/>.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not a managed assembly.</exception>
    public static AssemblyApiSurface Read(string assemblyPath)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}", assemblyPath);

        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                throw new InvalidDataException($"'{assemblyPath}' is not a managed assembly.");

            var reader = peReader.GetMetadataReader();
            var types = new Dictionary<string, ApiTypeInfo>(StringComparer.Ordinal);
            foreach (var handle in reader.TypeDefinitions)
            {
                var definition = reader.GetTypeDefinition(handle);
                if (definition.GetDeclaringType().IsNil)
                    Visit(reader, definition, parentFullName: null, types);
            }

            return new AssemblyApiSurface(types);
        }
        catch (BadImageFormatException ex)
        {
            throw new InvalidDataException($"'{assemblyPath}' is not a valid managed assembly: {ex.Message}", ex);
        }
    }

    private static void Visit(MetadataReader reader, TypeDefinition definition, string? parentFullName, Dictionary<string, ApiTypeInfo> types)
    {
        var visibility = definition.Attributes & TypeAttributes.VisibilityMask;
        var expected = parentFullName is null ? TypeAttributes.Public : TypeAttributes.NestedPublic;
        if (visibility != expected)
            return;

        var name = reader.GetString(definition.Name);
        var ns = reader.GetString(definition.Namespace);
        var qualifier = parentFullName ?? ns;
        var fullName = qualifier.Length == 0 ? name : $"{qualifier}.{name}";

        types[fullName] = new ApiTypeInfo(fullName, GetKind(reader, definition), CollectMemberNames(reader, definition));

        foreach (var nested in definition.GetNestedTypes())
            Visit(reader, reader.GetTypeDefinition(nested), fullName, types);
    }

    private static ApiTypeKind GetKind(MetadataReader reader, TypeDefinition definition)
    {
        var attributes = definition.Attributes;
        if ((attributes & TypeAttributes.Interface) != 0)
            return ApiTypeKind.Interface;

        switch (GetBaseTypeName(reader, definition.BaseType))
        {
            case "System.Enum":
                return ApiTypeKind.Enum;
            case "System.ValueType":
                return ApiTypeKind.Struct;
            case "System.MulticastDelegate":
                return ApiTypeKind.Delegate;
        }

        if (HasCloneMethod(reader, definition))
            return ApiTypeKind.Record;

        const TypeAttributes staticFlags = TypeAttributes.Abstract | TypeAttributes.Sealed;
        return (attributes & staticFlags) == staticFlags ? ApiTypeKind.StaticClass : ApiTypeKind.Class;
    }

    private static string GetBaseTypeName(MetadataReader reader, EntityHandle baseType)
    {
        if (baseType.Kind != HandleKind.TypeReference)
            return "";

        var reference = reader.GetTypeReference((TypeReferenceHandle)baseType);
        return $"{reader.GetString(reference.Namespace)}.{reader.GetString(reference.Name)}";
    }

    private static bool HasCloneMethod(MetadataReader reader, TypeDefinition definition)
    {
        foreach (var handle in definition.GetMethods())
        {
            if (reader.StringComparer.Equals(reader.GetMethodDefinition(handle).Name, "<Clone>$"))
                return true;
        }

        return false;
    }

    private static HashSet<string> CollectMemberNames(MetadataReader reader, TypeDefinition definition)
    {
        var isInterface = (definition.Attributes & TypeAttributes.Interface) != 0;
        var isSealed = (definition.Attributes & TypeAttributes.Sealed) != 0;
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in definition.GetMethods())
        {
            if (isInterface || IsVisible(reader.GetMethodDefinition(handle).Attributes, isSealed))
                names.Add(GetMethodName(reader, handle));
        }

        foreach (var handle in definition.GetFields())
        {
            var field = reader.GetFieldDefinition(handle);
            if (isInterface || IsVisible((int)(field.Attributes & FieldAttributes.FieldAccessMask), isSealed))
                names.Add(reader.GetString(field.Name));
        }

        foreach (var handle in definition.GetProperties())
        {
            var accessors = reader.GetPropertyDefinition(handle).GetAccessors();
            if (isInterface || AnyVisible(reader, isSealed, [accessors.Getter, accessors.Setter, .. accessors.Others]))
                names.Add(reader.GetString(reader.GetPropertyDefinition(handle).Name));
        }

        foreach (var handle in definition.GetEvents())
        {
            var accessors = reader.GetEventDefinition(handle).GetAccessors();
            if (isInterface || AnyVisible(reader, isSealed, [accessors.Adder, accessors.Remover, accessors.Raiser, .. accessors.Others]))
                names.Add(reader.GetString(reader.GetEventDefinition(handle).Name));
        }

        return names;
    }

    private static bool AnyVisible(MetadataReader reader, bool isSealed, IEnumerable<MethodDefinitionHandle> accessors) =>
        accessors.Any(h => !h.IsNil && IsVisible(reader.GetMethodDefinition(h).Attributes, isSealed));

    private static bool IsVisible(MethodAttributes attributes, bool isSealed) =>
        IsVisible((int)(attributes & MethodAttributes.MemberAccessMask), isSealed);

    // Method and field access masks share the same values: Public = 6, Family = 4, FamORAssem = 5.
    private static bool IsVisible(int access, bool isSealed)
    {
        const int family = (int)MethodAttributes.Family;
        const int familyOrAssembly = (int)MethodAttributes.FamORAssem;
        return access == (int)MethodAttributes.Public || (!isSealed && (access == family || access == familyOrAssembly));
    }

    private static string GetMethodName(MetadataReader reader, MethodDefinitionHandle handle)
    {
        var name = reader.GetString(reader.GetMethodDefinition(handle).Name);
        return name switch
        {
            ".ctor" => "#ctor",
            ".cctor" => "#cctor",
            _ => name,
        };
    }
}
