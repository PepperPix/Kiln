namespace Kiln.Services;

using Kiln.Models;

public enum WriteResult
{
    Written,
    SkippedAdopted,
    Conflict,
}

public interface IGeneratedContentWriter
{
    WriteResult Write(string outputDir, GeneratedContentFile file);
}
