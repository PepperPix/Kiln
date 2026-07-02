namespace Kiln.Services;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        CancellationToken ct);
}

public sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
