namespace Kiln.Services;

/// <summary>
/// Runs external processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process to completion and captures its standard output and standard error.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="workingDirectory">The working directory, or <see langword="null"/> for the default.</param>
    /// <param name="ct">A token to cancel the operation. Cancelling terminates the process.</param>
    /// <returns>The exit code and the captured output.</returns>
    Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        CancellationToken ct);
}
