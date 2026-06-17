namespace Kiln.Cli.Tests;

public class SmokeTests
{
    [Test]
    public async Task Placeholder_TUnit_Works()
    {
        await Assert.That(true).IsTrue();
    }
}
