namespace DesktopPet.Core;

public static class NoteTextOffsets
{
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
