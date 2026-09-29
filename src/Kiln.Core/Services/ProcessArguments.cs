namespace Kiln.Services;

using System.Text;

/// <summary>
/// Builds command-line argument strings for <see cref="System.Diagnostics.ProcessStartInfo.Arguments"/>.
/// </summary>
internal static class ProcessArguments
{
    private const int EscapeFactor = 2;

    /// <summary>
    /// Quotes a single argument following the <c>CommandLineToArgvW</c> rules used by .NET:
    /// embedded quotes become <c>\"</c> and backslashes before a quote or the closing quote are doubled.
    /// </summary>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');

        var backslashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
                sb.Append('\\', (backslashes * EscapeFactor) + 1).Append('"');
            else
                sb.Append('\\', backslashes).Append(ch);

            backslashes = 0;
        }

        sb.Append('\\', backslashes * EscapeFactor).Append('"');
        return sb.ToString();
    }
}
