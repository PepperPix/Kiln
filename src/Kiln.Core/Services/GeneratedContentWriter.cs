namespace Kiln.Services;

using System.Text;
using Kiln.Models;

public sealed class GeneratedContentWriter : IGeneratedContentWriter
{
    public WriteResult Write(string outputDir, GeneratedContentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var fullPath = Path.Combine(outputDir, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var hash = GeneratedContentSerializer.ComputeBodyHash(file.Body);

        var frontMatterWithHash = new List<(string Key, object Value)>(file.FrontMatter)
        {
            ("source_hash", hash),
        };

        var newContent = GeneratedContentSerializer.Serialize(frontMatterWithHash, file.Body);

        if (!File.Exists(fullPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, newContent, Encoding.UTF8);
            return WriteResult.Written;
        }

        var existingContent = File.ReadAllText(fullPath, Encoding.UTF8);
        var (isGenerated, storedHash, existingBody) = ParseExistingFile(existingContent);

        if (!isGenerated)
            return WriteResult.SkippedAdopted;

        var existingBodyHash = GeneratedContentSerializer.ComputeBodyHash(existingBody);

        if (string.Equals(existingBodyHash, storedHash, StringComparison.Ordinal))
        {
            File.WriteAllText(fullPath, newContent, Encoding.UTF8);
            return WriteResult.Written;
        }

        var regeneratedPath = fullPath + ".regenerated";
        File.WriteAllText(regeneratedPath, newContent, Encoding.UTF8);
        return WriteResult.Conflict;
    }

    private static (bool IsGenerated, string? StoredHash, string Body) ParseExistingFile(string content)
    {
        if (!content.StartsWith("---\n", StringComparison.Ordinal))
            return (false, null, content);

        var endIndex = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIndex < 0)
            return (false, null, content);

        var frontMatterText = content[4..endIndex];
        var bodyStart = endIndex + 5;

        if (bodyStart < content.Length && content[bodyStart] == '\n')
            bodyStart++;

        var body = bodyStart <= content.Length ? content[bodyStart..] : string.Empty;

        var isGenerated = false;
        string? storedHash = null;

        foreach (var rawLine in frontMatterText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var colonIdx = line.IndexOf(':', StringComparison.Ordinal);
            if (colonIdx < 0)
                continue;

            var key = line[..colonIdx].Trim();
            var val = line[(colonIdx + 1)..].Trim();

            if (string.Equals(key, "generated", StringComparison.Ordinal))
                isGenerated = string.Equals(val, "true", StringComparison.Ordinal);
            else if (string.Equals(key, "source_hash", StringComparison.Ordinal))
                storedHash = val;
        }

        return (isGenerated, storedHash, body);
    }
}
