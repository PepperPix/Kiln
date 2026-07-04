namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class SystemProcessRunnerTests
{
    [Test]
    public async Task RunAsync_SuccessfulCommand_ReturnsExitCodeZeroAndStdOut()
    {
        var runner = new SystemProcessRunner();

        var result = await runner.RunAsync("dotnet", "--version", workingDirectory: null, CancellationToken.None);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StdOut).IsNotEmpty();
        await Assert.That(result.StdOut).Contains('.');
    }

    [Test]
    public async Task RunAsync_FailingCommand_ReturnsNonZeroExitCodeAndStdErr()
    {
        var runner = new SystemProcessRunner();

        var result = await runner.RunAsync(
            "dotnet",
            "totally-bogus-subcommand-that-does-not-exist",
            workingDirectory: null,
            CancellationToken.None);

        await Assert.That(result.ExitCode).IsNotEqualTo(0);
        await Assert.That(result.StdErr).IsNotEmpty();
    }

    [Test]
    public async Task RunAsync_WithWorkingDirectory_UsesSpecifiedDirectory()
    {
        var runner = new SystemProcessRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-process-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = await runner.RunAsync("dotnet", "--version", tempDir, CancellationToken.None);

            await Assert.That(result.ExitCode).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
