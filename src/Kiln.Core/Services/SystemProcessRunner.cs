namespace Kiln.Services;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>
/// Default <see cref="IProcessRunner"/> implementation based on <see cref="System.Diagnostics.Process"/>.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on <see cref="IProcessRunner"/> instead.
/// </remarks>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            var stdOut = await stdOutTask.ConfigureAwait(false);
            var stdErr = await stdErrTask.ConfigureAwait(false);

            return new ProcessRunResult(process.ExitCode, stdOut, stdErr);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            await ObserveAsync(stdOutTask, stdErrTask).ConfigureAwait(false);
            throw;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
        catch (Win32Exception)
        {
            // Process is terminating or access was denied; nothing more to do.
        }
    }

    // The read tasks are cancelled together with the run; observe them so no exception goes unobserved.
    private static async Task ObserveAsync(Task stdOutTask, Task stdErrTask)
    {
        foreach (var task in new[] { stdOutTask, stdErrTask })
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected after cancellation.
            }
            catch (IOException)
            {
                // Pipe closed while the process was being killed.
            }
        }
    }
}
