namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class PluginNamesTests
{
    [Test]
    [Arguments("email-protect")]
    [Arguments("a")]
    [Arguments("0plugin")]
    [Arguments("my.plugin_v2")]
    [Arguments("a..b")]
    public async Task IsValid_WithAllowedName_ReturnsTrue(string name)
    {
        await Assert.That(PluginNames.IsValid(name)).IsTrue();
    }

    [Test]
    public async Task IsValid_WithMaximumLength_ReturnsTrue()
    {
        await Assert.That(PluginNames.IsValid(new string('a', 64))).IsTrue();
        await Assert.That(PluginNames.IsValid(new string('a', 65))).IsFalse();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments(".")]
    [Arguments("..")]
    [Arguments("../x")]
    [Arguments("..\\x")]
    [Arguments("a/b")]
    [Arguments("a\\b")]
    [Arguments("/abs")]
    [Arguments("C:\\abs")]
    [Arguments("-leading")]
    [Arguments(".hidden")]
    [Arguments("_leading")]
    [Arguments("Upper")]
    [Arguments("with space")]
    [Arguments("trailing\n")]
    [Arguments("nul\0char")]
    [Arguments("ümlaut")]
    public async Task IsValid_WithDisallowedName_ReturnsFalse(string? name)
    {
        await Assert.That(PluginNames.IsValid(name)).IsFalse();
    }
}
