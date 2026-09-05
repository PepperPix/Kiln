namespace Kiln.Core.Tests.Services;

using System.Collections.ObjectModel;
using Kiln.Models;
using Kiln.Services;

public class ShortcodeProcessorTests
{
    [Test]
    public async Task Process_ReplacesSingleShortcode()
    {
        var tempDir = CreatePluginWithShortcode("email-protect", "email", "<a href=\"mailto:{{ arg0 }}\">{{ arg0 }}</a>");
        try
        {
            var warnings = new Collection<string>();
            var processor = new ShortcodeProcessor();
            var result = processor.Process("Contact {% email \"hello@cscharf.de\" %} now.",
                [new PluginDefinition { Name = "Email Protect", Directory = tempDir, Shortcodes = ["email"] }],
                warnings);

            await Assert.That(result).Contains("<a href=\"mailto:hello@cscharf.de\">hello@cscharf.de</a>");
            await Assert.That(warnings.Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Process_ReplacesMultipleShortcodesInSameBody()
    {
        var tempDir = CreatePluginWithShortcode("email-protect", "email", "<span>{{ arg0 }}</span>");
        try
        {
            var warnings = new Collection<string>();
            var processor = new ShortcodeProcessor();
            var result = processor.Process("A {% email \"one@example.com\" %} / B {% email \"two@example.com\" %}",
                [new PluginDefinition { Name = "Email Protect", Directory = tempDir, Shortcodes = ["email"] }],
                warnings);

            await Assert.That(result).Contains("<span>one@example.com</span>");
            await Assert.That(result).Contains("<span>two@example.com</span>");
            await Assert.That(result).DoesNotContain("{% email");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Process_SkipsShortcodesInsideFencedCodeBlocks()
    {
        var tempDir = CreatePluginWithShortcode("email-protect", "email", "<span>{{ arg0 }}</span>");
        try
        {
            var warnings = new Collection<string>();
            var processor = new ShortcodeProcessor();
            var result = processor.Process(
                "```\n{% email \"demo@example.com\" %}\n```\nVisible {% email \"hello@example.com\" %}",
                [new PluginDefinition { Name = "Email Protect", Directory = tempDir, Shortcodes = ["email"] }],
                warnings);

            await Assert.That(result).Contains("{% email \"demo@example.com\" %}");
            await Assert.That(result).Contains("<span>hello@example.com</span>");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Process_LeavesUnknownShortcodeAsLiteralAndWarns()
    {
        var tempDir = CreatePluginWithShortcode("email-protect", "email", "<span>{{ arg0 }}</span>");
        try
        {
            var warnings = new Collection<string>();
            var processor = new ShortcodeProcessor();
            var result = processor.Process("{% mystery \"value\" %}",
                [new PluginDefinition { Name = "Email Protect", Directory = tempDir, Shortcodes = ["email"] }],
                warnings);

            await Assert.That(result).Contains("{% mystery \"value\" %}");
            await Assert.That(warnings).Contains("Unknown shortcode 'mystery' — no plugin declares it");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Process_ParsesQuotedArgumentAsSingleToken()
    {
        var tempDir = CreatePluginWithShortcode("email-protect", "email", "<span>{{ arg0 }}</span>");
        try
        {
            var warnings = new Collection<string>();
            var processor = new ShortcodeProcessor();
            var result = processor.Process("{% email \"hello world@example.com\" %}",
                [new PluginDefinition { Name = "Email Protect", Directory = tempDir, Shortcodes = ["email"] }],
                warnings);

            await Assert.That(result).Contains("<span>hello world@example.com</span>");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreatePluginWithShortcode(string pluginName, string shortcodeName, string partialContent)
    {
        var root = Path.Combine(Path.GetTempPath(), $"kiln-shortcode-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(root, pluginName);
        Directory.CreateDirectory(Path.Combine(pluginDir, "shortcodes"));
        File.WriteAllText(Path.Combine(pluginDir, "shortcodes", $"{shortcodeName}.html"), partialContent);
        return pluginDir;
    }
}
