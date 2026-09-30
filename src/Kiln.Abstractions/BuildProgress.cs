namespace Kiln.Abstractions;

/// <summary>
/// A progress report emitted while a site is being built.
/// </summary>
/// <param name="Phase">A human-readable description of the current build phase.</param>
/// <param name="Completed">The number of items completed in the phase.</param>
/// <param name="Total">The total number of items in the phase.</param>
public sealed record BuildProgress(string Phase, int Completed, int Total);
