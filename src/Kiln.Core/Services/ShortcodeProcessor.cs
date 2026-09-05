namespace Kiln.Services;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Kiln.Models;
using Scriban;
using Scriban.Runtime;

public sealed partial class ShortcodeProcessor : IShortcodeProcessor
{
    [GeneratedRegex(@"{%\s*(\S+)\s*(.*?)\s*%}", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ShortcodeRegex();

    [GeneratedRegex(@"^\s*([`~]{3,})", RegexOptions.Multiline)]
    private static partial Regex FenceMarkerRegex();

    public string Process(string markdown, IReadOnlyList<PluginDefinition> plugins, Collection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(warnings);

        if (string.IsNullOrEmpty(markdown))
            return markdown;

        var matches = ShortcodeRegex().Matches(markdown);
        if (matches.Count == 0)
            return markdown;

        var output = new StringBuilder(markdown.Length);
        var lastIndex = 0;

        foreach (Match match in matches)
        {
            output.Append(markdown, lastIndex, match.Index - lastIndex);

            if (IsInsideFencedCodeBlock(markdown, match.Index))
            {
                output.Append(match.Value);
            }
            else
            {
                var shortcodeName = match.Groups[1].Value.Trim();
                var rawArguments = match.Groups[2].Value.Trim();
                if (!TryRenderShortcode(shortcodeName, rawArguments, plugins, warnings, out var rendered))
                    output.Append(match.Value);
                else
                    output.Append(rendered);
            }

            lastIndex = match.Index + match.Length;
        }

        output.Append(markdown, lastIndex, markdown.Length - lastIndex);
        return output.ToString();
    }

    private static bool TryRenderShortcode(string shortcodeName, string rawArguments, IReadOnlyList<PluginDefinition> plugins, Collection<string> warnings, out string rendered)
    {
        rendered = string.Empty;

        var plugin = plugins.FirstOrDefault(p =>
            p.Shortcodes.Any(s => string.Equals(s, shortcodeName, StringComparison.OrdinalIgnoreCase)));
        if (plugin is null)
        {
            warnings.Add($"Unknown shortcode '{shortcodeName}' — no plugin declares it");
            return false;
        }

        var pluginKey = Path.GetFileName(plugin.Directory) ?? plugin.Name;
        var shortcodePath = Path.Combine(plugin.Directory, "shortcodes", $"{shortcodeName}.html");
        if (!File.Exists(shortcodePath))
        {
            warnings.Add($"Plugin '{pluginKey}' declares shortcode '{shortcodeName}' but no partial was found at '{shortcodePath}'.");
            return false;
        }

        var scriptObject = new ScriptObject();
        var args = TokenizeArguments(rawArguments);
        scriptObject.Add("args", args);
        for (var i = 0; i < args.Count; i++)
            scriptObject.Add($"arg{i}", args[i]);

        scriptObject.Add("plugin_name", pluginKey);
        scriptObject.Import("plugin_asset_url", new Func<string, string>(path =>
            $"/assets/plugins/{pluginKey.Trim('/').TrimStart('/')}/{path.TrimStart('/')}"));

        var stringFunctions = new ScriptObject();
        stringFunctions.Import("base64_encode", new Func<object?, string>(value =>
        {
            var raw = value is null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }));
        scriptObject.Add("string", stringFunctions);

        var template = Template.Parse(File.ReadAllText(shortcodePath), shortcodePath);
        if (template.HasErrors)
        {
            warnings.Add($"Could not render shortcode '{shortcodeName}' from '{shortcodePath}': {string.Join("; ", template.Messages.Select(m => m.Message))}");
            return false;
        }

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        rendered = template.Render(context);
        return true;
    }

    private static bool IsInsideFencedCodeBlock(string markdown, int matchIndex)
    {
        var prefix = markdown[..matchIndex];
        var inFence = false;
        char fenceChar = '\0';
        var fenceLength = 0;

        foreach (var line in prefix.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var fenceMatch = FenceMarkerRegex().Match(line);
            if (!fenceMatch.Success)
                continue;

            var marker = fenceMatch.Groups[1].Value;
            if (!inFence)
            {
                inFence = true;
                fenceChar = marker[0];
                fenceLength = marker.Length;
                continue;
            }

            if (marker[0] == fenceChar && marker.Length >= fenceLength)
                inFence = false;
        }

        return inFence;
    }

    private static List<string> TokenizeArguments(string rawArguments)
    {
        if (string.IsNullOrWhiteSpace(rawArguments))
            return [];

        var tokens = new List<string>();
        var token = new StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';

        foreach (var ch in rawArguments)
        {
            if ((ch == '"' || ch == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = ch;
                continue;
            }

            if (ch == quoteChar && inQuotes)
            {
                inQuotes = false;
                quoteChar = '\0';
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
                continue;
            }

            token.Append(ch);
        }

        if (token.Length > 0)
            tokens.Add(token.ToString());

        return tokens;
    }
}
