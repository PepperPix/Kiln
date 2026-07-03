namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class XmlDocParserTests
{
    private const int ExpectedMemberCount = 5;
    private const int ExpectedParamCount = 2;

    private const string SampleXml = """
        <?xml version="1.0"?>
        <doc>
            <assembly>
                <name>SampleLib</name>
            </assembly>
            <members>
                <member name="T:SampleLib.Widget">
                    <summary>Represents a widget.</summary>
                </member>
                <member name="M:SampleLib.Widget.Resize(System.Int32,System.Int32)">
                    <summary>Resizes the widget. See <see cref="T:SampleLib.Widget"/> for details.</summary>
                    <param name="width">New <paramref name="width"/> value.</param>
                    <param name="height">New height.</param>
                    <returns>True if resized.</returns>
                    <exception cref="T:System.ArgumentException">Thrown if negative.</exception>
                </member>
                <member name="P:SampleLib.Widget.Name">
                    <summary>The widget's name, e.g. <c>MyWidget</c>.</summary>
                </member>
                <member name="T:SampleLib.Repo`1">
                    <summary>
                    A generic repository.
                    <para>Supports basic CRUD.</para>
                    </summary>
                </member>
                <member name="M:SampleLib.Repo`1.Get(`0)">
                    <summary>Gets an item.</summary>
                    <remarks>
                    <code>
        var x = repo.Get(id);
                    </code>
                    </remarks>
                </member>
            </members>
        </doc>
        """;

    private static string WriteTempXml()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kiln-xmldoc-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, SampleXml);
        return path;
    }

    [Test]
    public async Task Parse_ReadsAssemblyNameAndAllMembers()
    {
        var path = WriteTempXml();
        try
        {
            var result = XmlDocParser.Parse(path);

            await Assert.That(result.AssemblyName).IsEqualTo("SampleLib");
            await Assert.That(result.Warnings).IsEmpty();
            await Assert.That(result.Members.Count).IsEqualTo(ExpectedMemberCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Parse_TypeMember_HasOwnerEqualToItself()
    {
        var path = WriteTempXml();
        try
        {
            var result = XmlDocParser.Parse(path);
            var typeMember = result.Members.Single(m => m.Kind == XmlDocMemberKind.Type && m.OwnerTypeFullName == "SampleLib.Widget");

            await Assert.That(typeMember.Summary).IsEqualTo("Represents a widget.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Parse_MethodMember_ExtractsOwnerSignatureParamsReturnsAndExceptions()
    {
        var path = WriteTempXml();
        try
        {
            var result = XmlDocParser.Parse(path);
            var method = result.Members.Single(m =>
                m.Kind == XmlDocMemberKind.Method && m.OwnerTypeFullName == "SampleLib.Widget");

            await Assert.That(method.MemberSignature).IsEqualTo("Resize(System.Int32,System.Int32)");
            await Assert.That(method.Summary).Contains("Resizes the widget.");
            await Assert.That(method.Summary).Contains("`SampleLib.Widget`");
            await Assert.That(method.Params.Count).IsEqualTo(ExpectedParamCount);
            await Assert.That(method.Params[0].Name).IsEqualTo("width");
            await Assert.That(method.Params[0].Text).Contains("`width`");
            await Assert.That(method.Returns).IsEqualTo("True if resized.");
            await Assert.That(method.Exceptions.Count).IsEqualTo(1);
            await Assert.That(method.Exceptions[0].Cref).IsEqualTo("System.ArgumentException");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Parse_PropertyMember_TransformsInlineCodeElement()
    {
        var path = WriteTempXml();
        try
        {
            var result = XmlDocParser.Parse(path);
            var property = result.Members.Single(m => m.Kind == XmlDocMemberKind.Property);

            await Assert.That(property.OwnerTypeFullName).IsEqualTo("SampleLib.Widget");
            await Assert.That(property.MemberSignature).IsEqualTo("Name");
            await Assert.That(property.Summary).Contains("`MyWidget`");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Parse_GenericType_KeepsBacktickArityInOwnerName()
    {
        var path = WriteTempXml();
        try
        {
            var result = XmlDocParser.Parse(path);
            var typeMember = result.Members.Single(m =>
                m.Kind == XmlDocMemberKind.Type && m.OwnerTypeFullName == "SampleLib.Repo`1");

            await Assert.That(typeMember.Summary).Contains("A generic repository.");
            await Assert.That(typeMember.Summary).Contains("Supports basic CRUD.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Parse_GenericMethod_ResolvesOwnerFromBacktickArityType()
    {
        var path = WriteTempXml();
        try
        {
            var result = XmlDocParser.Parse(path);
            var method = result.Members.Single(m =>
                m.Kind == XmlDocMemberKind.Method && m.OwnerTypeFullName == "SampleLib.Repo`1");

            await Assert.That(method.OwnerTypeFullName).IsEqualTo("SampleLib.Repo`1");
            await Assert.That(method.MemberSignature).IsEqualTo("Get(`0)");
            await Assert.That(method.Summary).IsEqualTo("Gets an item.");
            await Assert.That(method.Remarks).Contains("```");
            await Assert.That(method.Remarks).Contains("var x = repo.Get(id);");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Parse_MemberWithInvalidNamePrefix_IsSkippedAndReportedAsWarning()
    {
        const string xml = """
            <doc>
                <assembly><name>Bad</name></assembly>
                <members>
                    <member name="X:Bad.Unknown"><summary>Bad kind.</summary></member>
                    <member name="T:Bad.Widget"><summary>Fine.</summary></member>
                </members>
            </doc>
            """;
        var path = Path.Combine(Path.GetTempPath(), $"kiln-xmldoc-bad-{Guid.NewGuid():N}.xml");
        try
        {
            await File.WriteAllTextAsync(path, xml);
            var result = XmlDocParser.Parse(path);

            await Assert.That(result.Members.Count).IsEqualTo(1);
            await Assert.That(result.Warnings.Count).IsEqualTo(1);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
