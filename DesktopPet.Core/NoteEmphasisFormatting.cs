using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteEmphasisFormatting
{
    private readonly record struct Marker(string Text, NoteTextStyle Style);
    private readonly record struct Opening(Marker Marker, int Start, int ContentStart);
    private readonly record struct Pair(Opening Opening, int ClosingStart, int ClosingLength);

    public static NotesDocument Apply(NotesDocument document, (int Start, int Length) input,
        ref int selectionStart, ref int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(document);
        var documentLength = document.GetPlainText().Length;
        if (input.Start < 0 || input.Length < 0 || input.Start > documentLength - input.Length)
            throw new ArgumentOutOfRangeException(nameof(input), "The new input range is outside the note.");

        var result = new NotesDocument { Paragraphs = [] };
        var removed = new List<(int Start, int Length)>();
        var paragraphStart = 0;
        var insideFence = false;
        var insideDisplayMath = false;

        foreach (var row in document.Paragraphs)
        {
            var text = NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)));
            if (row.Kind is NoteBlockKind.Code or NoteBlockKind.DisplayMath)
            {
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }

            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph && !insideFence &&
                NoteCodeFormatting.IsOpeningFence(text))
            {
                insideFence = true;
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }
            if (insideFence && row.Cell is null)
            {
                if (row.Kind == NoteBlockKind.Paragraph && IsClosingFence(text)) insideFence = false;
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }
            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph && !insideDisplayMath && text.Trim() == "$$")
            {
                insideDisplayMath = true;
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }
            if (insideDisplayMath && row.Cell is null)
            {
                if (row.Kind == NoteBlockKind.Paragraph && text.Trim() == "$$") insideDisplayMath = false;
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }

            GetCharacterStyles(row.Runs, text.Length, out var styles, out var kinds, out var destinations);
            var code = styles.Select(style => style.HasFlag(NoteTextStyle.Code)).ToArray();
            var protectedSource = kinds.Select(kind => kind != NoteInlineKind.Text).ToArray();
            ProtectMathSource(text, code, protectedSource);
            var pairs = FindPairs(text, code, protectedSource, input, paragraphStart);
            if (pairs.Count == 0)
            {
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }

            var omitted = new bool[text.Length];
            var additions = new NoteTextStyle[text.Length];
            foreach (var pair in pairs)
            {
                for (var index = pair.Opening.Start; index < pair.Opening.ContentStart; index++)
                    omitted[index] = true;
                for (var index = pair.ClosingStart; index < pair.ClosingStart + pair.ClosingLength; index++)
                    omitted[index] = true;
                for (var index = pair.Opening.ContentStart; index < pair.ClosingStart; index++)
                    additions[index] |= pair.Opening.Marker.Style;
                removed.Add((paragraphStart + pair.Opening.Start, pair.Opening.ContentStart - pair.Opening.Start));
                removed.Add((paragraphStart + pair.ClosingStart, pair.ClosingLength));
            }

            var runs = new List<NoteTextRun>();
            for (var index = 0; index < text.Length;)
            {
                if (omitted[index])
                {
                    index++;
                    continue;
                }

                var runStart = index;
                var style = styles[index] | additions[index];
                var kind = kinds[index];
                var destination = destinations[index];
                index++;
                while (index < text.Length && !omitted[index] &&
                       (styles[index] | additions[index]) == style && kinds[index] == kind &&
                       destinations[index] == destination)
                    index++;
                runs.Add(new NoteTextRun(text[runStart..index], style, kind, destination));
            }

            result.Paragraphs.Add(new NoteParagraph
            {
                Cell = row.Cell,
                Kind = row.Kind,
                IsChecked = row.IsChecked,
                Runs = runs
            });
            paragraphStart += text.Length + 2;
        }

        if (removed.Count == 0) return document;
        var selectionEnd = MapOffset(selectionStart + selectionLength);
        selectionStart = MapOffset(selectionStart);
        selectionLength = selectionEnd - selectionStart;
        return result;

        int MapOffset(int offset) => offset - removed.Sum(range => Math.Clamp(offset - range.Start, 0, range.Length));
    }

    private static void GetCharacterStyles(IReadOnlyList<NoteTextRun> runs, int length,
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

    private static List<Pair> FindPairs(string text, bool[] code, bool[] protectedSource, (int Start, int Length) input, int paragraphStart)
    {
        var openings = new List<Opening>();
        var pairs = new List<Pair>();
        for (var index = 0; index < text.Length;)
        {
            if (text[index] is '\r' or '\n' || code[index] || protectedSource[index])
            {
                index++;
                continue;
            }
            if (IsEscaped(text, index))
            {
                var escapedMarker = text[index];
                if (escapedMarker is '*' or '_' or '~')
                {
                    while (index < text.Length && text[index] == escapedMarker && !code[index] && !protectedSource[index])
                        index++;
                }
                else index++;
                continue;
            }
            if (!TryGetMarker(text, index, openings, code, protectedSource, out var marker, out var runStart))
            {
                index++;
                continue;
            }

            var canClose = openings.Count > 0 &&
                openings[^1].Marker.Text == marker.Text &&
                CanClose(text, runStart, openings[^1].ContentStart);
            if (canClose)
            {
                var opening = openings[^1];
                openings.RemoveAt(openings.Count - 1);
                if (OverlapsInput(paragraphStart + opening.Start, opening.Marker.Text.Length, input) ||
                    OverlapsInput(paragraphStart + index, marker.Text.Length, input))
                    pairs.Add(new Pair(opening, index, marker.Text.Length));
                index += marker.Text.Length;
                continue;
            }

            if (IsEmptyPair(text, index, marker))
            {
                index += marker.Text.Length * 2;
                continue;
            }

            if (CanOpen(text, index, marker.Text.Length) &&
                !openings.Any(opening => opening.Marker.Text == marker.Text))
            {
                openings.Add(new Opening(marker, index, index + marker.Text.Length));
            }
            index += marker.Text.Length;
        }
        return pairs;
    }

    private static bool TryGetMarker(string text, int index, IReadOnlyList<Opening> openings, bool[] code,
        bool[] protectedSource, out Marker marker, out int runStart)
    {
        runStart = index;
        if (text[index] == '*')
        {
            var count = CountRun(text, index, '*', code, protectedSource);
            var top = openings.Count == 0 ? string.Empty : openings[^1].Marker.Text;
            if (count >= 3 && top == "***")
            {
                marker = new Marker("***", NoteTextStyle.Bold | NoteTextStyle.Italic);
            }
            else if (count >= 3 && top == "**")
            {
                marker = new Marker("**", NoteTextStyle.Bold);
            }
            else if (count >= 3 && top == "*" && !CanOpen(text, index, 3))
            {
                marker = new Marker("*", NoteTextStyle.Italic);
            }
            else if (count >= 3)
            {
                marker = new Marker("***", NoteTextStyle.Bold | NoteTextStyle.Italic);
            }
            else if (count >= 2 && top == "*" && !CanOpen(text, index, 2))
            {
                marker = new Marker("*", NoteTextStyle.Italic);
            }
            else if (count >= 2)
            {
                marker = new Marker("**", NoteTextStyle.Bold);
            }
            else
            {
                marker = new Marker("*", NoteTextStyle.Italic);
            }

            while (runStart > 0 && text[runStart - 1] == '*') runStart--;
            return true;
        }

        if ((text[index] is '_' or '~') && index + 1 < text.Length &&
            text[index + 1] == text[index] && !code[index + 1] && !protectedSource[index + 1])
        {
            marker = text[index] == '_'
                ? new Marker("__", NoteTextStyle.Underline)
                : new Marker("~~", NoteTextStyle.Strikethrough);
            return true;
        }

        marker = default;
        return false;
    }

    private static int CountRun(string text, int index, char character, bool[] code, bool[] protectedSource)
    {
        var end = index;
        while (end < text.Length && text[end] == character && !code[end] && !protectedSource[end])
            end++;
        return end - index;
    }

    private static bool CanOpen(string text, int index, int length) =>
        index + length < text.Length && text[index + length] is not ('\r' or '\n') &&
        !char.IsWhiteSpace(text[index + length]);

    private static bool CanClose(string text, int runStart, int contentStart) =>
        runStart > contentStart && runStart > 0 &&
        text[runStart - 1] is not ('\r' or '\n') && !char.IsWhiteSpace(text[runStart - 1]);

    private static bool IsEmptyPair(string text, int index, Marker marker)
    {
        var end = index + marker.Text.Length * 2;
        return end <= text.Length &&
            text.AsSpan(index, marker.Text.Length).SequenceEqual(marker.Text.AsSpan()) &&
            text.AsSpan(index + marker.Text.Length, marker.Text.Length).SequenceEqual(marker.Text.AsSpan()) &&
            !IsEscaped(text, index + marker.Text.Length);
    }

    private static bool OverlapsInput(int start, int length, (int Start, int Length) input) =>
        input.Length > 0 && start < input.Start + input.Length && start + length > input.Start;

    private static bool IsEscaped(string text, int index)
    {
        var slashes = 0;
        for (var cursor = index - 1; cursor >= 0 && text[cursor] == '\\'; cursor--) slashes++;
        return slashes % 2 != 0;
    }

    private static bool IsClosingFence(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length == 3 && trimmed.All(character => character == '\x60');
    }

    private static void ProtectMathSource(string text, bool[] code, bool[] protectedSource)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '$' || code[index] || protectedSource[index] || IsEscaped(text, index)) continue;
            var length = index + 1 < text.Length && text[index + 1] == '$' ? 2 : 1;
            var closing = FindClosingDollar(text, index + length, length, code);
            if (closing < 0)
            {
                Array.Fill(protectedSource, true, index + length, text.Length - index - length);
                return;
            }

            Array.Fill(protectedSource, true, index + length, closing - index - length);
            index = closing + length - 1;
        }
    }

    private static int FindClosingDollar(string text, int start, int length, bool[] code)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] != '$' || code[index] || IsEscaped(text, index)) continue;
            var candidateLength = index + 1 < text.Length && text[index + 1] == '$' ? 2 : 1;
            if (candidateLength == length && (length == 1 || !code[index + 1]))
                return index;
        }
        return -1;
    }
}
