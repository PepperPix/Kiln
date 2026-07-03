namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class XmlDocGeneratorTests
{
    private const int ExpectedTypeCount = 2;

    private const string SampleXml = """
        <?xml version="1.0"?>
        <doc>
            <assembly>
                <name>SampleLib</name>
            </assembly>
            <members>
                <member name="T:SampleLib.Widgets.Widget">
                    <summary>Represents a widget.</summary>
                </member>
                <member name="P:SampleLib.Widgets.Widget.Name">
                    <summary>The widget's name.</summary>
                </member>
                <member name="M:SampleLib.Widgets.Widget.Resize(System.Int32,System.Int32)">
                    <summary>Resizes the widget.</summary>
                    <param name="width">New width.</param>
                    <param name="height">New height.</param>
                    <returns>True if resized.</returns>
                    <exception cref="T:System.ArgumentException">Thrown if negative.</exception>
                </member>
                <member name="T:SampleLib.Repository">
                    <summary>Stores items.</summary>
                </member>
                <member name="F:SampleLib.Repository.Capacity">
                    <summary>Max item count.</summary>
                </member>
            </members>
        </doc>
        """;

    [Test]
    public async Task Generate_WithTwoTypes_CreatesOneFilePerTypeUnderNamespacePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-xmlgen-{Guid.NewGuid():N}");
        var xmlPath = Path.Combine(Path.GetTempPath(), $"kiln-xmlgen-src-{Guid.NewGuid():N}.xml");

        try
        {
            await File.WriteAllTextAsync(xmlPath, SampleXml);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new XmlDocGenerator(writer);

            var report = generator.Generate(xmlPath, tempDir);

            await Assert.That(report.Warnings).IsEmpty();
            await Assert.That(report.Conflicts).IsEmpty();
            await Assert.That(report.Skipped).IsEmpty();
            await Assert.That(report.Written.Count).IsEqualTo(ExpectedTypeCount);

            var widgetPath = Path.Combine(tempDir, "SampleLib", "Widgets", "Widget.md");
            await Assert.That(File.Exists(widgetPath)).IsTrue();
            var widgetContent = await File.ReadAllTextAsync(widgetPath);

            await Assert.That(widgetContent).Contains("title: Widget");
            await Assert.That(widgetContent).Contains("generated: true");
            await Assert.That(widgetContent).Contains("source_hash:");
            await Assert.That(widgetContent).Contains("namespace: SampleLib.Widgets");
            await Assert.That(widgetContent).Contains("assembly: SampleLib");
            await Assert.That(widgetContent).Contains("# Widget");
            await Assert.That(widgetContent).Contains("`SampleLib.Widgets.Widget`");
            await Assert.That(widgetContent).Contains("Represents a widget.");
            await Assert.That(widgetContent).Contains("## Properties");
            await Assert.That(widgetContent).Contains("### Name");
            await Assert.That(widgetContent).Contains("## Methods");
            await Assert.That(widgetContent).Contains("### Resize(Int32, Int32)");
            await Assert.That(widgetContent).Contains("**Parameters:**");
            await Assert.That(widgetContent).Contains("`width`");
            await Assert.That(widgetContent).Contains("**Returns:** True if resized.");
            await Assert.That(widgetContent).Contains("**Exceptions:**");
            await Assert.That(widgetContent).Contains("ArgumentException");

            var repoPath = Path.Combine(tempDir, "SampleLib", "Repository.md");
            await Assert.That(File.Exists(repoPath)).IsTrue();
            var repoContent = await File.ReadAllTextAsync(repoPath);

            await Assert.That(repoContent).Contains("title: Repository");
            await Assert.That(repoContent).Contains("namespace: SampleLib");
            await Assert.That(repoContent).Contains("## Fields");
            await Assert.That(repoContent).Contains("### Capacity");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(xmlPath))
                File.Delete(xmlPath);
        }
    }

    [Test]
    public async Task Generate_TypesAreOrderedAlphabeticallyWithSequentialWeight()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-xmlgen-order-{Guid.NewGuid():N}");
        var xmlPath = Path.Combine(Path.GetTempPath(), $"kiln-xmlgen-order-src-{Guid.NewGuid():N}.xml");

        try
        {
            await File.WriteAllTextAsync(xmlPath, SampleXml);
            Directory.CreateDirectory(tempDir);

            var writer = new GeneratedContentWriter();
            var generator = new XmlDocGenerator(writer);
            generator.Generate(xmlPath, tempDir);

            var repoContent = await File.ReadAllTextAsync(Path.Combine(tempDir, "SampleLib", "Repository.md"));
            var widgetContent = await File.ReadAllTextAsync(Path.Combine(tempDir, "SampleLib", "Widgets", "Widget.md"));

            // "SampleLib.Repository" sorts before "SampleLib.Widgets.Widget" ordinally.
            await Assert.That(repoContent).Contains("weight: 1");
            await Assert.That(widgetContent).Contains("weight: 2");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(xmlPath))
                File.Delete(xmlPath);
        }
    }

    [Test]
    public async Task Generate_FileNotFound_ThrowsFileNotFoundException()
    {
        var writer = new GeneratedContentWriter();
        var generator = new XmlDocGenerator(writer);
        var missingPath = Path.Combine(Path.GetTempPath(), $"kiln-missing-{Guid.NewGuid():N}.xml");

        await Assert.That(() => generator.Generate(missingPath, Path.GetTempPath()))
            .ThrowsExactly<FileNotFoundException>();
    }
}
