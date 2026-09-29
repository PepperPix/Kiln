namespace Kiln.Services;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        CancellationToken ct);
}
