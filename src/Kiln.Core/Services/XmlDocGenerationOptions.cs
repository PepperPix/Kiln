namespace Kiln.Services;

/// <summary>
/// Options for <see cref="IXmlDocGenerator.Generate(string, string, XmlDocGenerationOptions)"/>.
/// </summary>
/// <param name="AssemblyPath">The path of the compiled assembly. When set, only publicly visible types and members are documented and each page names the kind of the type.</param>
public sealed record XmlDocGenerationOptions(string? AssemblyPath = null);
