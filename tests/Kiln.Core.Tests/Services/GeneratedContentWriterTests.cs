namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class GeneratedContentWriterTests
{
    private static GeneratedContentFile MakeFile(string relativePath, string body)
        => new(
            relativePath,
            [("title", "Test Operation"), ("weight", 1), ("generated", true)],
            body);

    [Test]
    public async Task Write_NewFile_CreatesFileAndReturnsWritten()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-writer-new-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new GeneratedContentWriter();
            var file = MakeFile("ops/list.md", "# List\n\n`GET /items`\n");

            var result = writer.Write(dir, file);

            await Assert.That(result).IsEqualTo(WriteResult.Written);
            var fullPath = Path.Combine(dir, "ops", "list.md");
            await Assert.That(File.Exists(fullPath)).IsTrue();
            var content = await File.ReadAllTextAsync(fullPath);
            await Assert.That(content).Contains("generated: true");
            await Assert.That(content).Contains("source_hash:");
            await Assert.That(content).Contains("# List");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Write_ExistingGeneratedFile_SameBody_OverwritesAndReturnsWritten()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-writer-overwrite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new GeneratedContentWriter();
            var file = MakeFile("ops/list.md", "# List\n\n`GET /items`\n");

            var result1 = writer.Write(dir, file);
            await Assert.That(result1).IsEqualTo(WriteResult.Written);

            // Write same file again — hash matches → overwrite
            var result2 = writer.Write(dir, file);
            await Assert.That(result2).IsEqualTo(WriteResult.Written);

            var fullPath = Path.Combine(dir, "ops", "list.md");
            var content = await File.ReadAllTextAsync(fullPath);
            await Assert.That(content).Contains("# List");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Write_ExistingAdoptedFile_SkipsAndReturnsSkippedAdopted()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-writer-adopted-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new GeneratedContentWriter();
            var subDir = Path.Combine(dir, "ops");
            Directory.CreateDirectory(subDir);

            // Pre-create a file without "generated: true" (adopted by user)
            var fullPath = Path.Combine(subDir, "list.md");
            const string adoptedContent = "---\ntitle: My Custom List\n---\n\nCustom content.\n";
            await File.WriteAllTextAsync(fullPath, adoptedContent);

            var file = MakeFile("ops/list.md", "# Generated List\n\n`GET /items`\n");
            var result = writer.Write(dir, file);

            await Assert.That(result).IsEqualTo(WriteResult.SkippedAdopted);

            // Original file must be untouched
            var content = await File.ReadAllTextAsync(fullPath);
            await Assert.That(content).IsEqualTo(adoptedContent);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Write_ExistingGeneratedFile_ModifiedBody_WritesRegeneratedAndReturnsConflict()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-writer-conflict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new GeneratedContentWriter();
            const string originalBody = "# List\n\n`GET /items`\n";
            var file = MakeFile("ops/list.md", originalBody);

            // Write initial file
            var result1 = writer.Write(dir, file);
            await Assert.That(result1).IsEqualTo(WriteResult.Written);

            var fullPath = Path.Combine(dir, "ops", "list.md");
            var writtenContent = await File.ReadAllTextAsync(fullPath);

            // Simulate user edit: modify body keeping frontmatter intact
            var modifiedContent = SimulateBodyEdit(writtenContent, "# List (edited by user)\n\n`GET /items`\n");
            await File.WriteAllTextAsync(fullPath, modifiedContent);

            // Try to write again — hash mismatch → conflict
            var result2 = writer.Write(dir, file);
            await Assert.That(result2).IsEqualTo(WriteResult.Conflict);

            // .regenerated file must exist
            await Assert.That(File.Exists(fullPath + ".regenerated")).IsTrue();

            // Original must be preserved (user's edit intact)
            var originalNow = await File.ReadAllTextAsync(fullPath);
            await Assert.That(originalNow).Contains("edited by user");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string SimulateBodyEdit(string fileContent, string newBody)
    {
        if (!fileContent.StartsWith("---\n", StringComparison.Ordinal))
            return fileContent;

        var separatorIdx = fileContent.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (separatorIdx < 0)
            return fileContent;

        var bodyStart = separatorIdx + 5;
        if (bodyStart < fileContent.Length && fileContent[bodyStart] == '\n')
            bodyStart++;

        return fileContent[..bodyStart] + newBody;
    }
}
