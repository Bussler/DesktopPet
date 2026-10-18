namespace DesktopPet.Core;

public static class NoteTextOffsets
{
    internal static void GetCharacterData(IReadOnlyList<NoteTextRun> runs, int length,
        out NoteTextStyle[] styles, out NoteInlineKind[] kinds, out string?[] destinations)
    {
        styles = new NoteTextStyle[length];
        kinds = new NoteInlineKind[length];
        destinations = new string?[length];
        var offset = 0;
        foreach (var run in runs)
        {
            var runLength = NormalizeLineBreaks(run.Text).Length;
            for (var index = 0; index < runLength && offset < length; index++, offset++)
            {
                styles[offset] = run.Style;
                kinds[offset] = run.Kind;
                destinations[offset] = run.LinkDestination;
            }
        }
    }

    // Stored plain-text offsets count every line break as CRLF.
    public static int RawOffset(string text, int normalizedOffset)
    {
        var rawOffset = 0;
        var currentOffset = 0;
        while (rawOffset < text.Length && currentOffset < normalizedOffset)
        {
            if (text[rawOffset] == '\r' && rawOffset + 1 < text.Length && text[rawOffset + 1] == '\n')
            {
                rawOffset += 2;
                currentOffset += 2;
            }
            else if (text[rawOffset] is '\r' or '\n')
            {
                rawOffset++;
                currentOffset += 2;
            }
            else
            {
                rawOffset++;
                currentOffset++;
            }
        }

        return rawOffset;
    }

    public static string NormalizeLineBreaks(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", "\r\n", StringComparison.Ordinal);
}
