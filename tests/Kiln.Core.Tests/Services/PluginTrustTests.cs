namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class PluginTrustTests
{
    [Test]
    [Arguments("Kiln.Plugin.EmailProtect", true, PluginTrustLevel.FirstParty)]
    [Arguments("kiln.plugin.emailprotect", true, PluginTrustLevel.FirstParty)]
    [Arguments("KILN.PLUGIN.Seo", true, PluginTrustLevel.FirstParty)]
    [Arguments("Kiln.Plugin.EmailProtect", false, PluginTrustLevel.Community)]
    [Arguments("Contoso.Kiln.Plugin.Widget", true, PluginTrustLevel.Community)]
    [Arguments("Contoso.Widget", true, PluginTrustLevel.Community)]
    [Arguments("Contoso.Widget", false, PluginTrustLevel.Community)]
    [Arguments("Kiln.Plugin", true, PluginTrustLevel.Community)]
    public async Task Classify_ReturnsFirstPartyOnlyForReservedKilnPluginPrefix(string packageId, bool prefixReserved, PluginTrustLevel expected)
    {
        var level = PluginTrust.Classify(packageId, prefixReserved);

        await Assert.That(level).IsEqualTo(expected);
    }
}
