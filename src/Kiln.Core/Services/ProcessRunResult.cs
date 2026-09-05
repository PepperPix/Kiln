namespace Kiln.Services;

public sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
