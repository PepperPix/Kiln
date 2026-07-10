namespace Kiln.Cli.Tests;

using TUnit.Core;

/// <summary>
/// See Kiln.Core.Tests.GlobalTestSetup for the rationale — bounds a single hung test's runtime
/// instead of relying solely on the CI job-level timeout.
/// </summary>
public static class GlobalTestSetup
{
    private static readonly TimeSpan DefaultTestTimeout = TimeSpan.FromSeconds(60);

    [Before(HookType.TestDiscovery)]
    public static Task Configure(BeforeTestDiscoveryContext context)
    {
        context.Settings.Timeouts.DefaultTestTimeout = DefaultTestTimeout;
        return Task.CompletedTask;
    }
}
