namespace Kiln.Cli.Tests;

using System.Diagnostics;

public class GenDocsSmokeTests
{
    private const string MinimalSpec = """
        openapi: "3.0.0"
        info:
          title: "Smoke API"
          version: "1.0.0"
        paths:
          /items:
            get:
              tags: ["items"]
              operationId: "listItems"
              summary: "List items"
              responses:
                "200":
                  description: "OK"
        """;

    [Test]
    public async Task GenDocsCommand_WithValidSpec_ExitsZeroAndCreatesContentFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-smoke-gendocs-{Guid.NewGuid():N}");
        var specPath = Path.Combine(Path.GetTempPath(), $"kiln-spec-{Guid.NewGuid():N}.yaml");
        var outputDir = Path.Combine(tempDir, "out");

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(specPath, MinimalSpec);

            var cliDll = Path.Combine(
                Path.GetDirectoryName(typeof(GenDocsSmokeTests).Assembly.Location)!,
                "Kiln.Cli.dll");

            var psi = new ProcessStartInfo(
                "dotnet",
                $"exec \"{cliDll}\" gen docs --openapi \"{specPath}\" --output \"{outputDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            await Assert.That(process.ExitCode).IsEqualTo(0);
            await Assert.That(File.Exists(Path.Combine(outputDir, "items", "listItems.md"))).IsTrue();
            await Assert.That(output).Contains("written");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (File.Exists(specPath))
                File.Delete(specPath);
        }
    }
}
