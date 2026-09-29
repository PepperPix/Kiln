namespace Kiln.Core.Tests.Services;

using System.Diagnostics;
using TUnit.Core.Enums;
using Kiln.Services;

public class SystemProcessRunnerCancellationTests
{
    // Unix only: relies on /bin/sh and a background `sleep` grandchild; there is no equivalent shell on Windows runners.
    [Test]
    [ExcludeOn(OS.Windows)]
    public async Task RunAsync_Cancelled_KillsChildProcessTree()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-proc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var pidFile = Path.Combine(dir, "pid.txt");
        var sleeperPid = 0;
        try
        {
            using var cts = new CancellationTokenSource();
            var runner = new SystemProcessRunner();
            var script = $"sleep 60 & echo $! > '{pidFile}'; wait";
            var run = runner.RunAsync("/bin/sh", $"-c \"{script}\"", null, cts.Token);

            sleeperPid = await WaitForPidAsync(pidFile);
            await cts.CancelAsync();

            await Assert.That(run).Throws<OperationCanceledException>();
            await Assert.That(await WaitUntilGoneAsync(sleeperPid)).IsTrue();
        }
        finally
        {
            if (sleeperPid > 0)
                KillIfRunning(sleeperPid);
            Directory.Delete(dir, true);
        }
    }

    private static async Task<int> WaitForPidAsync(string pidFile)
    {
        for (var i = 0; i < 100; i++)
        {
            if (File.Exists(pidFile)
                && int.TryParse((await File.ReadAllTextAsync(pidFile)).Trim(), out var pid)
                && pid > 0)
            {
                return pid;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Child process did not write its PID.");
    }

    private static async Task<bool> WaitUntilGoneAsync(int pid)
    {
        for (var i = 0; i < 100; i++)
        {
            if (!IsRunning(pid))
                return true;

            await Task.Delay(50);
        }

        return false;
    }

    private static bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void KillIfRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }
}
