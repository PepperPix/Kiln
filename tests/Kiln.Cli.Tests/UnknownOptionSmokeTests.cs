namespace Kiln.Cli.Tests;

using System.Diagnostics;

public class UnknownOptionSmokeTests
{
    [Test]
    public async Task Build_UnknownOption_FailsAndDoesNotBuild()
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"kiln-strict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var (newExit, _) = await RunCliAsync(workDir, "new demo");
            var siteDir = Path.Combine(workDir, "demo");

            var (exitCode, output) = await RunCliAsync(siteDir, "build --bogus");

            await Assert.That(newExit).IsEqualTo(0);
            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("bogus");
            await Assert.That(Directory.Exists(Path.Combine(siteDir, "_site"))).IsFalse();
        }
        finally
        {
            Directory.Delete(workDir, true);
        }
    }

    [Test]
    public async Task New_UnknownOption_FailsAndCreatesNothing()
    {
        var workDir = Path.Combine(Path.GetTempPath(), $"kiln-strict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            var (exitCode, output) = await RunCliAsync(workDir, "new demo --bogus");

            await Assert.That(exitCode).IsNotEqualTo(0);
            await Assert.That(output).Contains("bogus");
            await Assert.That(Directory.Exists(Path.Combine(workDir, "demo"))).IsFalse();
        }
        finally
        {
            Directory.Delete(workDir, true);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunCliAsync(string workingDirectory, string arguments)
    {
        var cliDll = Path.Combine(
            Path.GetDirectoryName(typeof(UnknownOptionSmokeTests).Assembly.Location)!,
            "Kiln.Cli.dll");

        var psi = new ProcessStartInfo("dotnet", $"exec \"{cliDll}\" {arguments}")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdout + await stderr);
    }
}
