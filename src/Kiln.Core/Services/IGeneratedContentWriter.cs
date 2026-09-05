namespace Kiln.Services;

using Kiln.Models;

public interface IGeneratedContentWriter
{
    WriteResult Write(string outputDir, GeneratedContentFile file);
}
