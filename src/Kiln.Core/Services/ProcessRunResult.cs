namespace Kiln.Services;

/// <summary>
/// The outcome of running a process with <see cref="IProcessRunner"/>.
/// </summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="StdOut">The captured standard output.</param>
/// <param name="StdErr">The captured standard error.</param>
public sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
