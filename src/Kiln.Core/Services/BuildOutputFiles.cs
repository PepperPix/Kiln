namespace Kiln.Services;

using System.Text;

internal static class BuildOutputFiles
{
    internal static async Task WriteOutputTextAsync(
        string outputPath,
        string content,
        HashSet<string>? generatedFiles,
        CancellationToken ct,
        Encoding? encoding = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (encoding is null)
            await File.WriteAllTextAsync(outputPath, content, ct).ConfigureAwait(false);
        else
            await File.WriteAllTextAsync(outputPath, content, encoding, ct).ConfigureAwait(false);

        generatedFiles?.Add(Path.GetFullPath(outputPath));
    }

    internal static void PruneStaleOutputs(string projectPath, string outputDir, HashSet<string> generatedFiles)
    {
        OutputDirectoryGuard.EnsureNotProjectRootOrAncestor(projectPath, outputDir);

        if (!Directory.Exists(outputDir))
            return;

        foreach (var file in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(file);
            if (!generatedFiles.Contains(fullPath))
                File.Delete(fullPath);
        }

        foreach (var directory in Directory.GetDirectories(outputDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static path => path.Length))
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
                continue;
            Directory.Delete(directory);
        }
    }
}
