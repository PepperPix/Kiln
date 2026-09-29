namespace Kiln.Core.Tests;

using TUnit.Core;

/// <summary>
/// CI has occasionally seen a whole test run hang (not just fail) for hours on macOS/Windows
/// runners instead of finishing in the usual ~2-3s — e.g. a port-binding race in
/// <c>DevServerLiveReloadTests</c> leaving a listener stuck waiting forever. A single hung test
/// otherwise blocks the entire process indefinitely with no diagnostic beyond "the job never
/// finished". This sets a generous-but-bounded default per-test timeout so a hang fails fast with
/// a clear "which test timed out" message instead of relying solely on the CI job-level timeout
/// (belt-and-suspenders — see .github/workflows/ci.yml timeout-minutes).
///
/// 60s comfortably covers this project's slowest legitimate tests (DevServerLiveReloadTests can
/// chain multiple 8s network-readiness waits in a single test method) with several times headroom,
/// while still catching a true hang in under a minute rather than hours.
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
