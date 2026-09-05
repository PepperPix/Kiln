namespace Kiln.Abstractions;

public sealed record BuildProgress(string Phase, int Completed, int Total);
