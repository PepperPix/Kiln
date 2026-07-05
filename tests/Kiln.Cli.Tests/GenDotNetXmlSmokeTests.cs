namespace Kiln.Cli.Tests;

using System.Diagnostics;

public class GenDotNetXmlSmokeTests
{
    private const string MinimalXmlDoc = """
        <?xml version="1.0"?>
        <doc>
            <assembly><name>SmokeLib</name></assembly>
            <members>
                <member name="T:SmokeLib.Widget">
                    <summary>A widget.</summary>
                </member>
                <member name="P:SmokeLib.Widget.Name">
                    <summary>The name.</summary>
                </member>
            </members>
        </doc>
        """;

    [Test]
    public async Task GenDotNetXmlCommand_WithValidXml_ExitsZeroAndCreatesContentFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-smoke-genxml-{Guid.NewGuid():N}");
        var xmlPath = Path.Combine(Path.GetTempPath(), $"kiln-xmldoc-{Guid.NewGuid():N}.xml");
        var outputDir = Path.Combine(tempDir, "out");

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(xmlPath, MinimalXmlDoc);

            var cliDll = Path.Combine(
                Path.GetDirectoryName(typeof(GenDotNetXmlSmokeTests).Assembly.Location)!,
                "Kiln.Cli.dll");

            var psi = new ProcessStartInfo(
                "dotnet",
                $"exec \"{cliDll}\" gen dotnet-xml --xml \"{xmlPath}\" --output \"{outputDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            await Assert.That(process.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(Path.Combine(outputDir, "SmokeLib", "Widget.md"))).IsTrue();
            await Assert.That(output).Contains("written");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(xmlPath))
                File.Delete(xmlPath);
        }
    }
}
