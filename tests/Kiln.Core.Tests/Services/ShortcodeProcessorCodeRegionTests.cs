namespace Kiln.Core.Tests.Services;

using System.Collections.ObjectModel;
using System.Diagnostics;
using Kiln.Models;
using Kiln.Services;

public class ShortcodeProcessorCodeRegionTests
{
    private const string Sc = "{% email \"a@b.c\" %}";
    private const string Rendered = "<span>a@b.c</span>";

    [Test]
    [Arguments("Use `{% email \"a@b.c\" %}` to embed", "Use `{% email \"a@b.c\" %}` to embed")]
    [Arguments("Use ``{% email \"a@b.c\" %} with ` inside`` ok", "Use ``{% email \"a@b.c\" %} with ` inside`` ok")]
    [Arguments("`x` {% email \"a@b.c\" %} `y`", "`x` <span>a@b.c</span> `y`")]
    [Arguments("lone ` tick {% email \"a@b.c\" %}", "lone ` tick <span>a@b.c</span>")]
    [Arguments("``{% email \"a@b.c\" %}` unmatched runs", "``<span>a@b.c</span>` unmatched runs")]
    [Arguments("`open\n{% email \"a@b.c\" %}\nclose`", "`open\n<span>a@b.c</span>\nclose`")]
    [Arguments("```\n{% email \"a@b.c\" %}\n```\n{% email \"a@b.c\" %}", "```\n{% email \"a@b.c\" %}\n```\n<span>a@b.c</span>")]
    [Arguments("~~~\n{% email \"a@b.c\" %}\n~~~\n{% email \"a@b.c\" %}", "~~~\n{% email \"a@b.c\" %}\n~~~\n<span>a@b.c</span>")]
    [Arguments("```\n{% email \"a@b.c\" %}\n`````\n{% email \"a@b.c\" %}", "```\n{% email \"a@b.c\" %}\n`````\n<span>a@b.c</span>")]
    [Arguments("````\n```\n{% email \"a@b.c\" %}\n```\n````\n{% email \"a@b.c\" %}", "````\n```\n{% email \"a@b.c\" %}\n```\n````\n<span>a@b.c</span>")]
    [Arguments("```\n~~~\n{% email \"a@b.c\" %}\n~~~\n```\n{% email \"a@b.c\" %}", "```\n~~~\n{% email \"a@b.c\" %}\n~~~\n```\n<span>a@b.c</span>")]
    [Arguments("```\n{% email \"a@b.c\" %}\nnever closed {% email \"a@b.c\" %}", "```\n{% email \"a@b.c\" %}\nnever closed {% email \"a@b.c\" %}")]
    [Arguments("```\r\n{% email \"a@b.c\" %}\r\n```\r\n{% email \"a@b.c\" %}", "```\r\n{% email \"a@b.c\" %}\r\n```\r\n<span>a@b.c</span>")]
    [Arguments("  ```js\n`x` {% email \"a@b.c\" %}\n  ```\nafter {% email \"a@b.c\" %}", "  ```js\n`x` {% email \"a@b.c\" %}\n  ```\nafter <span>a@b.c</span>")]
    public async Task Process_ProtectsFencedBlocksAndSingleLineInlineCode(string input, string expected)
    {
        var result = Run(input);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Process_InlineCodeInsideFence_DoesNotBreakFenceTracking()
    {
        var result = Run("```\n`\n```\n" + Sc);

        await Assert.That(result).IsEqualTo("```\n`\n```\n" + Rendered);
    }

    [Test]
    public async Task Process_ManyShortcodesAndLines_ScalesLinearly()
    {
        const int small = 2500;
        const int large = 10000;

        Run(BuildInput(small));

        var smallTime = Measure(small, out _);
        var largeTime = Measure(large, out var result);

        await Assert.That(result).DoesNotContain("{% email");
        await Assert.That(result.Split(Rendered).Length - 1).IsEqualTo(large);

        // 4x input: linear is ~4x, quadratic ~16x; absolute times vary too much between CI runners.
        await Assert.That(largeTime.TotalMilliseconds).IsLessThan(Math.Max(smallTime.TotalMilliseconds, 50) * 10);
    }

    private static string BuildInput(int count)
    {
        var lines = new List<string>(count * 2);
        for (var i = 0; i < count; i++)
        {
            lines.Add($"Line {i} of text with `code` and {Sc}");
            lines.Add("plain filler line");
        }

        return string.Join('\n', lines);
    }

    private static TimeSpan Measure(int count, out string result)
    {
        var input = BuildInput(count);
        var stopwatch = Stopwatch.StartNew();
        result = Run(input);
        return stopwatch.Elapsed;
    }

    private static string Run(string markdown)
    {
        var root = Path.Combine(Path.GetTempPath(), $"kiln-shortcode-region-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(root, "email-protect");
        Directory.CreateDirectory(Path.Combine(pluginDir, "shortcodes"));
        File.WriteAllText(Path.Combine(pluginDir, "shortcodes", "email.html"), "<span>{{ arg0 }}</span>");
        try
        {
            return new ShortcodeProcessor().Process(
                markdown,
                [new PluginDefinition { Name = "Email Protect", Directory = pluginDir, Shortcodes = ["email"] }],
                new Collection<string>());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
