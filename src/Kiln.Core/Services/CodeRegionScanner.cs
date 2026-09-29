namespace Kiln.Services;

/// <summary>
/// Finds the parts of a Markdown document in which shortcodes must stay literal:
/// fenced code blocks and single-line inline code spans. One linear pass over the text.
/// </summary>
internal static class CodeRegionScanner
{
    private const int MinFenceLength = 3;

    /// <summary>
    /// Returns ascending, non-overlapping <c>[Start, End)</c> ranges of protected text.
    /// A fenced block starts after its opening marker and ends after its closing marker (or at the end of the text).
    /// </summary>
    public static List<(int Start, int End)> Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var regions = new List<(int Start, int End)>();
        var inFence = false;
        var fenceChar = '\0';
        var fenceLength = 0;
        var fenceStart = 0;
        var lineStart = 0;

        while (true)
        {
            var newline = text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? text.Length : newline;

            if (TryReadFenceMarker(text.AsSpan(lineStart, lineEnd - lineStart), out var markerOffset, out var markerLength))
            {
                var markerStart = lineStart + markerOffset;
                var markerEnd = markerStart + markerLength;
                if (!inFence)
                {
                    inFence = true;
                    fenceChar = text[markerStart];
                    fenceLength = markerLength;
                    fenceStart = markerEnd;
                }
                else if (text[markerStart] == fenceChar && markerLength >= fenceLength)
                {
                    inFence = false;
                    regions.Add((fenceStart, markerEnd));
                }
            }
            else if (!inFence)
            {
                AddInlineCodeSpans(text, lineStart, lineEnd, regions);
            }

            if (newline < 0)
                break;

            lineStart = newline + 1;
        }

        if (inFence)
            regions.Add((fenceStart, text.Length));

        return regions;
    }

    // Same rule as the former regex ^\s*([`~]{3,}): leading whitespace, then a run of at least three fence characters.
    private static bool TryReadFenceMarker(ReadOnlySpan<char> line, out int offset, out int length)
    {
        offset = 0;
        while (offset < line.Length && char.IsWhiteSpace(line[offset]))
            offset++;

        var end = offset;
        while (end < line.Length && (line[end] == '`' || line[end] == '~'))
            end++;

        length = end - offset;
        return length >= MinFenceLength;
    }

    private static void AddInlineCodeSpans(string text, int lineStart, int lineEnd, List<(int Start, int End)> regions)
    {
        var i = text.IndexOf('`', lineStart, lineEnd - lineStart);
        while (i >= 0 && i < lineEnd)
        {
            var runEnd = SkipBackticks(text, i, lineEnd);
            var closeEnd = FindClosingRun(text, runEnd, lineEnd, runEnd - i);
            if (closeEnd < 0)
            {
                i = NextBacktick(text, runEnd, lineEnd);
                continue;
            }

            regions.Add((i, closeEnd));
            i = NextBacktick(text, closeEnd, lineEnd);
        }
    }

    // Returns the end of the next backtick run with exactly the opening length, or -1 if the line has none.
    private static int FindClosingRun(string text, int from, int lineEnd, int runLength)
    {
        var j = NextBacktick(text, from, lineEnd);
        while (j >= 0)
        {
            var runEnd = SkipBackticks(text, j, lineEnd);
            if (runEnd - j == runLength)
                return runEnd;

            j = NextBacktick(text, runEnd, lineEnd);
        }

        return -1;
    }

    private static int SkipBackticks(string text, int from, int lineEnd)
    {
        var end = from;
        while (end < lineEnd && text[end] == '`')
            end++;

        return end;
    }

    private static int NextBacktick(string text, int from, int lineEnd) =>
        from >= lineEnd ? -1 : text.IndexOf('`', from, lineEnd - from);
}
