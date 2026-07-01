namespace Kiln.Services;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public static class GeneratedContentSerializer
{
    public static string Serialize(IReadOnlyList<(string Key, object Value)> frontMatter, string body)
    {
        ArgumentNullException.ThrowIfNull(frontMatter);
        ArgumentNullException.ThrowIfNull(body);

        var sb = new StringBuilder();
        sb.Append("---\n");

        foreach (var (key, value) in frontMatter)
            AppendYamlEntry(sb, key, value);

        sb.Append("---\n\n");
        sb.Append(body);
        return sb.ToString();
    }

    public static string ComputeBodyHash(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private static void AppendYamlEntry(StringBuilder sb, string key, object value)
    {
        switch (value)
        {
            case IDictionary<string, string> dict:
                sb.Append(key).Append(":\n");
                foreach (var kvp in dict)
                    sb.Append("  ").Append(kvp.Key).Append(": ").Append(QuoteYamlString(kvp.Value)).Append('\n');
                break;

            case bool b:
                sb.Append(key).Append(": ").Append(b ? "true" : "false").Append('\n');
                break;

            case int i:
                sb.Append(key).Append(": ").Append(i.ToString(CultureInfo.InvariantCulture)).Append('\n');
                break;

            default:
                sb.Append(key).Append(": ").Append(QuoteYamlString(value.ToString() ?? string.Empty)).Append('\n');
                break;
        }
    }

    private static string QuoteYamlString(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "\"\"";

        if (NeedsQuoting(s))
        {
            var escaped = s
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
            return $"\"{escaped}\"";
        }

        return s;
    }

    private static bool NeedsQuoting(string s)
    {
        if (string.IsNullOrEmpty(s))
            return true;
        if (ContainsSpecialYamlChar(s))
            return true;
        if (s[0] == ' ' || s[^1] == ' ')
            return true;
        return IsReservedWord(s) || char.IsDigit(s[0]);
    }

    private static bool ContainsSpecialYamlChar(string s)
        => s.IndexOfAny([':', '#', '{', '}', '[', ']', '|', '>', '!', '%', '@', '`', '\'', '"']) >= 0;

    private static bool IsReservedWord(string s)
        => s is "true" or "false" or "null" or "~";
}
