namespace Kiln.Core.Tests.Services;

using Kiln.Core.Tests.Fixtures;
using Kiln.Services;

public class XmlDocGeneratorAssemblyFilterTests
{
    private const string Ns = "Kiln.Core.Tests.Fixtures.ApiSurfaceFixtures";
    private const int VisibleTypeCount = 10;

    private static readonly string FixtureXml = """
        <?xml version="1.0"?>
        <doc>
            <assembly><name>Kiln.Core.Tests</name></assembly>
            <members>
                <member name="T:@NS@.IPublicContract"><summary>A contract.</summary></member>
                <member name="P:@NS@.IPublicContract.Name"><summary>The name.</summary></member>
                <member name="M:@NS@.IPublicContract.Execute"><summary>Runs it.</summary></member>
                <member name="T:@NS@.PublicMode"><summary>A mode.</summary></member>
                <member name="F:@NS@.PublicMode.First"><summary>First mode.</summary></member>
                <member name="T:@NS@.PublicRecord"><summary>A record.</summary></member>
                <member name="P:@NS@.PublicRecord.Value"><summary>The value.</summary></member>
                <member name="T:@NS@.PublicStruct"><summary>A struct.</summary></member>
                <member name="F:@NS@.PublicStruct.Size"><summary>The size.</summary></member>
                <member name="T:@NS@.PublicHandler"><summary>A handler.</summary></member>
                <member name="T:@NS@.PublicStatic"><summary>A static class.</summary></member>
                <member name="M:@NS@.PublicStatic.Helper"><summary>Helps.</summary></member>
                <member name="T:@NS@.PublicClass"><summary>A class.</summary></member>
                <member name="M:@NS@.PublicClass.#ctor"><summary>Creates it.</summary></member>
                <member name="M:@NS@.PublicClass.Visible"><summary>Visible method.</summary></member>
                <member name="M:@NS@.PublicClass.Hook"><summary>Protected hook.</summary></member>
                <member name="M:@NS@.PublicClass.HiddenMethod"><summary>Internal method.</summary></member>
                <member name="F:@NS@.PublicClass._secret"><summary>Private field.</summary></member>
                <member name="T:@NS@.PublicClass.NestedPublic"><summary>Nested public.</summary></member>
                <member name="M:@NS@.PublicClass.NestedPublic.Ping"><summary>Pings.</summary></member>
                <member name="T:@NS@.PublicClass.NestedInternal"><summary>Nested internal.</summary></member>
                <member name="M:@NS@.PublicClass.NestedInternal.Pong"><summary>Pongs.</summary></member>
                <member name="T:@NS@.SealedClass"><summary>A sealed class.</summary></member>
                <member name="M:@NS@.SealedClass.Hook"><summary>Sealed protected hook.</summary></member>
                <member name="T:@NS@.GenericBox`1"><summary>A generic box.</summary></member>
                <member name="P:@NS@.GenericBox`1.Value"><summary>The boxed value.</summary></member>
                <member name="M:@NS@.GenericBox`1.Map``1(System.Func{`0,``0})"><summary>Maps the value.</summary></member>
                <member name="T:@NS@.HiddenType"><summary>An internal type.</summary></member>
                <member name="M:@NS@.HiddenType.Reveal"><summary>Reveals.</summary></member>
                <member name="T:@NS@.HiddenType.InnerOfHidden"><summary>Inner of an internal type.</summary></member>
                <member name="M:@NS@.HiddenType.InnerOfHidden.Inside"><summary>Inside.</summary></member>
            </members>
        </doc>
        """.Replace("@NS@", Ns, StringComparison.Ordinal);

    private static string FixtureAssemblyPath => typeof(ApiSurfaceFixtures).Assembly.Location;

    private static string PagePath(string outputDir, string relativeToFixtures) =>
        Path.Combine(outputDir, "Kiln", "Core", "Tests", "Fixtures", "ApiSurfaceFixtures", relativeToFixtures);

    private static string NewTempPath() => Path.Combine(Path.GetTempPath(), $"kiln-xmlasm-{Guid.NewGuid():N}");

    private static async Task<T> WithFixtureAsync<T>(Func<string, string, Task<T>> action)
    {
        var tempDir = NewTempPath();
        var xmlPath = tempDir + ".xml";
        try
        {
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(xmlPath, FixtureXml);
            return await action(tempDir, xmlPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(xmlPath))
                File.Delete(xmlPath);
        }
    }

    private static XmlDocGenerator CreateGenerator() => new(new GeneratedContentWriter());

    [Test]
    public async Task Generate_WithAssembly_WritesOnlyPubliclyVisibleTypes()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            var report = CreateGenerator().Generate(xmlPath, tempDir, new XmlDocGenerationOptions(FixtureAssemblyPath));

            await Assert.That(report.Written.Count).IsEqualTo(VisibleTypeCount);
            await Assert.That(report.Warnings).IsEmpty();
            await Assert.That(report.Notes).Contains("Left out 3 non-public types and 6 non-public members.");
            await Assert.That(File.Exists(PagePath(tempDir, "PublicClass.md"))).IsTrue();
            await Assert.That(File.Exists(PagePath(tempDir, Path.Combine("PublicClass", "NestedPublic.md")))).IsTrue();
            await Assert.That(File.Exists(PagePath(tempDir, "HiddenType.md"))).IsFalse();
            await Assert.That(File.Exists(PagePath(tempDir, Path.Combine("HiddenType", "InnerOfHidden.md")))).IsFalse();
            await Assert.That(File.Exists(PagePath(tempDir, Path.Combine("PublicClass", "NestedInternal.md")))).IsFalse();
            return true;
        });
    }

    [Test]
    public async Task Generate_WithAssembly_ListsProtectedMembersOnlyOfUnsealedTypes()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            CreateGenerator().Generate(xmlPath, tempDir, new XmlDocGenerationOptions(FixtureAssemblyPath));

            var publicClass = await File.ReadAllTextAsync(PagePath(tempDir, "PublicClass.md"));
            await Assert.That(publicClass).Contains("### Visible()");
            await Assert.That(publicClass).Contains("### Hook()");
            await Assert.That(publicClass).Contains("### #ctor()");
            await Assert.That(publicClass).DoesNotContain("HiddenMethod");
            await Assert.That(publicClass).DoesNotContain("_secret");

            var sealedClass = await File.ReadAllTextAsync(PagePath(tempDir, "SealedClass.md"));
            await Assert.That(sealedClass).DoesNotContain("Hook");
            return true;
        });
    }

    [Test]
    [Arguments("IPublicContract.md", "interface")]
    [Arguments("PublicMode.md", "enum")]
    [Arguments("PublicRecord.md", "record")]
    [Arguments("PublicStruct.md", "struct")]
    [Arguments("PublicHandler.md", "delegate")]
    [Arguments("PublicStatic.md", "static class")]
    [Arguments("PublicClass.md", "class")]
    public async Task Generate_WithAssembly_NamesTheTypeKindInFrontMatterAndHeader(string page, string expectedKind)
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            CreateGenerator().Generate(xmlPath, tempDir, new XmlDocGenerationOptions(FixtureAssemblyPath));

            var content = await File.ReadAllTextAsync(PagePath(tempDir, page));
            await Assert.That(content).Contains($"kind: {expectedKind}");
            await Assert.That(content).Contains($"*({expectedKind})*");
            return true;
        });
    }

    [Test]
    public async Task Generate_WithAssembly_MatchesGenericAndNestedTypesByXmlName()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            CreateGenerator().Generate(xmlPath, tempDir, new XmlDocGenerationOptions(FixtureAssemblyPath));

            var generic = await File.ReadAllTextAsync(PagePath(tempDir, "GenericBox`1.md"));
            await Assert.That(generic).Contains("title: GenericBox<T>");
            await Assert.That(generic).Contains("### Value");
            await Assert.That(generic).Contains("### Map(");

            var nested = await File.ReadAllTextAsync(PagePath(tempDir, Path.Combine("PublicClass", "NestedPublic.md")));
            await Assert.That(nested).Contains("### Ping()");
            return true;
        });
    }

    [Test]
    public async Task Generate_WithoutAssembly_IsIdenticalToTheOptionlessOverload()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            var plainDir = Path.Combine(tempDir, "plain");
            var optionsDir = Path.Combine(tempDir, "options");

            var plain = CreateGenerator().Generate(xmlPath, plainDir);
            var withOptions = CreateGenerator().Generate(xmlPath, optionsDir, new XmlDocGenerationOptions());

            await Assert.That(withOptions.Written).IsEquivalentTo(plain.Written);
            await Assert.That(withOptions.Notes).IsEmpty();
            await Assert.That(plain.Written.Count).IsEqualTo(Directory.GetFiles(plainDir, "*.md", SearchOption.AllDirectories).Length);

            foreach (var relative in plain.Written)
            {
                var expected = await File.ReadAllTextAsync(Path.Combine(plainDir, relative));
                var actual = await File.ReadAllTextAsync(Path.Combine(optionsDir, relative));
                await Assert.That(actual).IsEqualTo(expected);
                await Assert.That(actual).DoesNotContain("kind:");
                await Assert.That(actual).DoesNotContain("*(");
            }

            return true;
        });
    }

    [Test]
    public async Task Generate_WithoutAssembly_ProducesTheKnownPageLayout()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            CreateGenerator().Generate(xmlPath, tempDir);

            var content = await File.ReadAllTextAsync(PagePath(tempDir, "PublicStruct.md"));
            await Assert.That(content).StartsWith("---\ntitle: PublicStruct\n");
            await Assert.That(content).Contains("\n---\n\n# PublicStruct\n\n`Kiln.Core.Tests.Fixtures.ApiSurfaceFixtures.PublicStruct`\n\nA struct.\n\n## Fields\n\n### Size\n\nThe size.\n");
            return true;
        });
    }

    [Test]
    public async Task Generate_WithMissingAssembly_ThrowsAndWritesNothing()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            var missing = Path.Combine(tempDir, "missing.dll");

            await Assert.That(() => CreateGenerator().Generate(xmlPath, tempDir, new XmlDocGenerationOptions(missing)))
                .Throws<FileNotFoundException>();
            await Assert.That(Directory.GetFileSystemEntries(tempDir)).IsEmpty();
            return true;
        });
    }

    [Test]
    public async Task Generate_WithInvalidAssembly_ThrowsInvalidDataAndWritesNothing()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            var bogus = xmlPath + ".dll";
            await File.WriteAllTextAsync(bogus, "not an assembly");
            try
            {
                await Assert.That(() => CreateGenerator().Generate(xmlPath, tempDir, new XmlDocGenerationOptions(bogus)))
                    .Throws<InvalidDataException>();
                await Assert.That(Directory.GetFileSystemEntries(tempDir)).IsEmpty();
            }
            finally
            {
                File.Delete(bogus);
            }

            return true;
        });
    }

    [Test]
    public async Task Generate_WithAssembly_SecondRunIsIdempotentAndEditedPagesGetRegeneratedFile()
    {
        await WithFixtureAsync(async (tempDir, xmlPath) =>
        {
            var options = new XmlDocGenerationOptions(FixtureAssemblyPath);
            CreateGenerator().Generate(xmlPath, tempDir, options);
            var pagePath = PagePath(tempDir, "PublicClass.md");
            var firstContent = await File.ReadAllTextAsync(pagePath);

            var second = CreateGenerator().Generate(xmlPath, tempDir, options);

            await Assert.That(second.Conflicts).IsEmpty();
            await Assert.That(await File.ReadAllTextAsync(pagePath)).IsEqualTo(firstContent);

            await File.WriteAllTextAsync(pagePath, firstContent + "\nManual edit.\n");
            var third = CreateGenerator().Generate(xmlPath, tempDir, options);

            await Assert.That(third.Conflicts.Count).IsEqualTo(1);
            await Assert.That(File.Exists(pagePath + ".regenerated")).IsTrue();
            return true;
        });
    }
}
