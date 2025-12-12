namespace Hex1b.Terminal;

internal static class AnsiString
{
    private const char Escape = '\x1b';

    public static int VisibleLength(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var visible = 0;
        for (var i = 0; i < text.Length;)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                i = nextIndex;
                continue;
            }

            visible++;
            i++;
        }

        return visible;
    }

    public static string SliceByColumns(string text, int startColumn, int lengthColumns)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (lengthColumns <= 0)
            return "";
        if (startColumn < 0)
            startColumn = 0;

        // Collect style codes that appear before the first included visible character.
        var prefix = new System.Text.StringBuilder();
        var output = new System.Text.StringBuilder();

        var visibleIndex = 0;
        var started = false;

        var endExclusive = startColumn + lengthColumns;

        var i = 0;
        for (; i < text.Length;)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                var seq = text.Substring(i, nextIndex - i);
                if (!started)
                    prefix.Append(seq);
                else
                    output.Append(seq);

                i = nextIndex;
                continue;
            }

            if (visibleIndex < startColumn)
            {
                visibleIndex++;
                i++;
                continue;
            }

            if (visibleIndex >= endExclusive)
                break;

            if (!started)
            {
                output.Append(prefix);
                started = true;
            }

            output.Append(text[i]);
            visibleIndex++;
            i++;
        }

        if (!started)
            return "";

        // Preserve any escape sequences that immediately follow the slice.
        // (Does not include additional printable characters.)
        for (; i < text.Length;)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                output.Append(text.Substring(i, nextIndex - i));
                i = nextIndex;
                continue;
            }

            break;
        }

        return output.ToString();
    }

    public static string TrailingEscapeSuffix(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Find the end index of the last printable character.
        var lastPrintableEnd = 0;
        for (var i = 0; i < text.Length;)
        {
            if (TryReadCsi(text, i, out var nextIndex))
            {
                i = nextIndex;
                continue;
            }

            // Treat any non-CSI as printable for our purposes.
            lastPrintableEnd = i + 1;
            i++;
        }

        if (lastPrintableEnd >= text.Length)
            return "";

        // Ensure suffix contains only full CSI sequences.
        for (var i = lastPrintableEnd; i < text.Length;)
        {
            if (!TryReadCsi(text, i, out var nextIndex))
                return "";

            i = nextIndex;
        }

        return text.Substring(lastPrintableEnd);
    }

    private static bool TryReadCsi(string text, int index, out int nextIndex)
    {
        nextIndex = index;
        if (index < 0 || index >= text.Length)
            return false;

        if (text[index] != Escape)
            return false;
        if (index + 1 >= text.Length || text[index + 1] != '[')
            return false;

        // CSI sequence: ESC [ ... <final byte>
        for (var i = index + 2; i < text.Length; i++)
        {
            var c = text[i];
            if (c >= '@' && c <= '~')
            {
                nextIndex = i + 1;
                return true;
            }
        }

        // Incomplete CSI sequence.
        return false;
    }
}
