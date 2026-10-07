using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteBlockMarkdownFormatting
{
    public static NotesDocument Apply(NotesDocument document, (int Start, int Length) input,
        ref int selectionStart, ref int selectionLength, bool pasted = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        var documentLength = document.GetPlainText().Length;
        if (input.Start < 0 || input.Length < 0 || input.Start > documentLength - input.Length)
            throw new ArgumentOutOfRangeException(nameof(input), "The new input range is outside the note.");

        var result = new NotesDocument { Paragraphs = [] };
        var removed = new List<(int Start, int Length)>();
        var appendedParagraphs = new List<int>();
        var offset = 0;
        var insideFence = false;
        var insideDisplayMath = false;

        foreach (var row in document.Paragraphs)
        {
            var text = NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)));
            if (row.Cell is not null || (row.Kind != NoteBlockKind.Paragraph && !IsHeadingKind(row.Kind)))
            {
                result.Paragraphs.Add(row.Copy());
                offset += text.Length + 2;
                continue;
            }

            if (insideFence)
            {
                result.Paragraphs.Add(row.Copy());
                if (IsClosingFence(text)) insideFence = false;
                offset += text.Length + 2;
                continue;
            }
            if (NoteCodeFormatting.IsOpeningFence(text) && !HasCodeStyle(row, 0, text.Length))
            {
                insideFence = true;
                result.Paragraphs.Add(row.Copy());
                offset += text.Length + 2;
                continue;
            }

            if (insideDisplayMath)
            {
                result.Paragraphs.Add(row.Copy());
                if (text.Trim() == "$$" && !HasCodeStyle(row, 0, text.Length)) insideDisplayMath = false;
                offset += text.Length + 2;
                continue;
            }
            if (text.Trim() == "$$" && !HasCodeStyle(row, 0, text.Length))
            {
                insideDisplayMath = true;
                result.Paragraphs.Add(row.Copy());
                offset += text.Length + 2;
                continue;
            }

            var headingLevel = GetHeadingLevel(text);
            var headingPrefixLength = headingLevel + (headingLevel > 0 ? 1 : 0);
            if (headingLevel > 0 && !HasCodeStyle(row, 0, headingPrefixLength) &&
                !HasLink(row, 0, headingPrefixLength) &&
                OverlapsInput(offset, headingPrefixLength, input))
            {
                var heading = row.Copy();
                heading.Kind = HeadingKind(headingLevel);
                heading.Runs = RemovePrefix(row.Runs, headingPrefixLength);
                result.Paragraphs.Add(heading);
                removed.Add((offset, headingPrefixLength));
            }
            else if (text == "---" && !HasCodeStyle(row, 0, text.Length) && !HasLink(row, 0, text.Length) &&
                     ((pasted && CoversInput(input, offset, text.Length)) || OverlapsInput(offset + text.Length, 2, input)))
            {
                var divider = row.Copy();
                divider.Kind = NoteBlockKind.Divider;
                divider.IsChecked = false;
                divider.Runs = [];
                result.Paragraphs.Add(divider);
                removed.Add((offset, text.Length));
                if (result.Paragraphs.Count == document.Paragraphs.Count &&
                    ReferenceEquals(row, document.Paragraphs[^1]))
                {
                    appendedParagraphs.Add(offset + text.Length);
                    result.Paragraphs.Add(new NoteParagraph());
                }
            }
            else
            {
                result.Paragraphs.Add(row.Copy());
            }

            offset += text.Length + 2;
        }

        if (removed.Count == 0) return document;
        var originalSelectionStart = selectionStart;
        var originalSelectionEnd = selectionStart + selectionLength;
        var mappedStart = MapOffset(originalSelectionStart);
        var mappedEnd = MapOffset(originalSelectionEnd);
        foreach (var position in appendedParagraphs)
        {
            if (originalSelectionStart >= position) mappedStart += 2;
            if (originalSelectionEnd >= position) mappedEnd += 2;
        }
        selectionStart = mappedStart;
        selectionLength = mappedEnd - mappedStart;
        return result;

        int MapOffset(int position) => position - removed.Sum(range => Math.Clamp(position - range.Start, 0, range.Length));
    }

    private static bool IsHeadingKind(NoteBlockKind kind) => kind is
        NoteBlockKind.Heading1 or NoteBlockKind.Heading2 or NoteBlockKind.Heading3 or
        NoteBlockKind.Heading4 or NoteBlockKind.Heading5 or NoteBlockKind.Heading6;

    private static int GetHeadingLevel(string text)
    {
        var count = 0;
        while (count < text.Length && text[count] == '#') count++;
        return count is >= 1 and <= 6 && count < text.Length && text[count] == ' ' ? count : 0;
    }

    private static NoteBlockKind HeadingKind(int level) => level switch
    {
        1 => NoteBlockKind.Heading1,
        2 => NoteBlockKind.Heading2,
        3 => NoteBlockKind.Heading3,
        4 => NoteBlockKind.Heading4,
        5 => NoteBlockKind.Heading5,
        6 => NoteBlockKind.Heading6,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    private static List<NoteTextRun> RemovePrefix(IReadOnlyList<NoteTextRun> runs, int length)
    {
        var remaining = length;
        var result = new List<NoteTextRun>();
        foreach (var run in runs)
        {
            var count = Math.Min(remaining, NormalizeLineBreaks(run.Text).Length);
            remaining -= count;
            var rawOffset = RawOffset(run.Text, count);
            if (rawOffset < run.Text.Length) result.Add(run with { Text = run.Text[rawOffset..] });
        }
        return result;
    }

    private static bool HasCodeStyle(NoteParagraph row, int start, int length)
    {
        var end = start + length;
        var offset = 0;
        foreach (var run in row.Runs)
        {
            var runEnd = offset + NormalizeLineBreaks(run.Text).Length;
            if (offset < end && runEnd > start && run.Style.HasFlag(NoteTextStyle.Code)) return true;
            offset = runEnd;
            if (offset >= end) break;
        }
        return false;
    }

    private static bool HasLink(NoteParagraph row, int start, int length)
    {
        var end = start + length;
        var offset = 0;
        foreach (var run in row.Runs)
        {
            var runEnd = offset + NormalizeLineBreaks(run.Text).Length;
            if (offset < end && runEnd > start && run.Kind == NoteInlineKind.Link) return true;
            offset = runEnd;
            if (offset >= end) break;
        }
        return false;
    }

    private static bool IsClosingFence(string text) =>
        text.Trim().Length == 3 && text.Trim().All(character => character == '\x60');

    private static bool CoversInput((int Start, int Length) input, int start, int length) =>
        input.Length > 0 && input.Start <= start && input.Start + input.Length >= start + length;

    private static bool OverlapsInput(int start, int length, (int Start, int Length) input) =>
        input.Length > 0 && start < input.Start + input.Length && start + length > input.Start;
}
