using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteDisplayMathFormatting
{
    private readonly record struct Formula(int Start, int End);
    private readonly record struct AddedBreak(int Before, int After, bool HasBefore, bool HasAfter, bool JumpAfter = false);

    public static NotesDocument Apply(NotesDocument document, (int Start, int Length) input,
        ref int selectionStart, ref int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateRange(document, input.Start, input.Length);

        var result = new NotesDocument { Paragraphs = [] };
        var addedBreaks = new List<AddedBreak>();
        var offset = 0;
        var insideFence = false;
        var insideDisplayMath = false;
        var changed = false;
        for (var index = 0; index < document.Paragraphs.Count; index++)
        {
            var row = document.Paragraphs[index];
            var text = GetText(row);
            if (row.Kind == NoteBlockKind.Code || row.Kind == NoteBlockKind.DisplayMath || row.Cell is not null ||
                !IsBodyTextKind(row.Kind))
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
                if (IsDisplayDelimiterLine(text)) insideDisplayMath = false;
                offset += text.Length + 2;
                continue;
            }

            if (row.Kind == NoteBlockKind.Paragraph && IsDisplayDelimiterLine(text) &&
                !HasCodeStyle(row, 0, text.Length))
            {
                var close = FindClosingDelimiterLine(document.Paragraphs, index + 1);
                if (close >= 0)
                {
                    var source = string.Join("\r\n", document.Paragraphs.Skip(index).Take(close - index + 1).Select(GetText));
                    var closeText = GetText(document.Paragraphs[close]);
                    var openStart = offset + text.IndexOf("$$", StringComparison.Ordinal);
                    var closeStart = offset + document.Paragraphs.Skip(index).Take(close - index)
                        .Sum(candidate => GetText(candidate).Length + 2) + closeText.IndexOf("$$", StringComparison.Ordinal);
                    if (OverlapsInput(openStart, 2, input) || OverlapsInput(closeStart, 2, input))
                    {
                        result.Paragraphs.Add(CreateDisplayBlock(source));
                        var groupEnd = closeStart + 2 + closeText[(closeText.IndexOf("$$", StringComparison.Ordinal) + 2)..].Length;
                        addedBreaks.Add(new AddedBreak(groupEnd, groupEnd, false, false, JumpAfter: true));
                        if (close == document.Paragraphs.Count - 1) result.Paragraphs.Add(new NoteParagraph());
                        changed = true;
                    }
                    else
                    {
                        for (var inner = index; inner <= close; inner++)
                            result.Paragraphs.Add(document.Paragraphs[inner].Copy());
                    }

                    while (index <= close)
                    {
                        offset += GetText(document.Paragraphs[index]).Length + 2;
                        index++;
                    }
                    index--;
                    insideDisplayMath = close < 0;
                    continue;
                }

                insideDisplayMath = true;
                result.Paragraphs.Add(row.Copy());
                offset += text.Length + 2;
                continue;
            }

            GetCharacterData(row.Runs, text.Length, out var styles, out var kinds);
            var formulas = FindSameLineFormulas(text, styles, kinds, input, offset);
            if (formulas.Count == 0)
            {
                result.Paragraphs.Add(row.Copy());
                offset += text.Length + 2;
                continue;
            }

            changed = true;
            var cursor = 0;
            foreach (var formula in formulas)
            {
                var hasBefore = formula.Start > cursor;
                var hasAfter = formula.End < text.Length;
                var jumpAfter = formula.End == text.Length;
                if (hasBefore) result.Paragraphs.Add(CreateTextFragment(row, cursor, formula.Start));
                result.Paragraphs.Add(CreateDisplayBlock(text[formula.Start..formula.End]));
                addedBreaks.Add(new AddedBreak(offset + formula.Start, offset + formula.End, hasBefore, hasAfter,
                    JumpAfter: jumpAfter));
                cursor = formula.End;
            }
            if (cursor < text.Length) result.Paragraphs.Add(CreateTextFragment(row, cursor, text.Length));
            else if (index == document.Paragraphs.Count - 1 && formulas[^1].End == text.Length)
                result.Paragraphs.Add(new NoteParagraph());
            offset += text.Length + 2;
        }

        if (!changed) return document;
        var originalEnd = selectionStart + selectionLength;
        var mappedStart = MapOffset(selectionStart);
        var mappedEnd = MapOffset(originalEnd);
        selectionStart = mappedStart;
        selectionLength = Math.Max(0, mappedEnd - mappedStart);
        return result;

        int MapOffset(int position) => position + addedBreaks.Sum(breaks =>
            (breaks.HasBefore && position >= breaks.Before ? 2 : 0) +
            (breaks.HasAfter && position >= breaks.After ? 2 : 0) +
            (breaks.JumpAfter && position == breaks.After ? 2 : 0));
    }

    public static NotesDocument ReplaceSelection(NotesDocument document, int start, int length, out int blockIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateRange(document, start, length);
        if (length == 0) throw new ArgumentOutOfRangeException(nameof(length), "Select note text to wrap in a display equation.");

        var source = document.GetPlainText().Substring(start, length);
        var (first, firstOffset) = document.GetParagraphPositionAt(start);
        var (last, lastOffset) = document.GetParagraphPositionAt(start + length);
        var paragraphs = document.Paragraphs.Take(first).Select(row => row.Copy()).ToList();
        var prefix = CreateFragment(document.Paragraphs[first], 0, firstOffset);
        if (prefix is not null) paragraphs.Add(prefix);
        blockIndex = paragraphs.Count;
        paragraphs.Add(CreateDisplayBlock(source));
        var lastLength = GetText(document.Paragraphs[last]).Length;
        var suffix = CreateFragment(document.Paragraphs[last], lastOffset, lastLength);
        if (suffix is not null) paragraphs.Add(suffix);
        paragraphs.AddRange(document.Paragraphs.Skip(last + 1).Select(row => row.Copy()));
        if (paragraphs.Count == 0) paragraphs.Add(new NoteParagraph());
        return new NotesDocument { Paragraphs = paragraphs };
    }

    public static NotesDocument InsertAtCaret(NotesDocument document, int offset, out int blockIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateRange(document, offset, 0);
        var (paragraphIndex, localOffset) = document.GetParagraphPositionAt(offset);
        var source = document.Paragraphs[paragraphIndex];
        var paragraphs = document.Paragraphs.Take(paragraphIndex).Select(row => row.Copy()).ToList();

        if (source.Cell is { } cell)
        {
            paragraphs.Add(source.Copy());
            var lastCellIndex = document.Paragraphs.FindLastIndex(row => row.Cell?.TableId == cell.TableId);
            paragraphs.AddRange(document.Paragraphs.Skip(paragraphIndex + 1).Take(lastCellIndex - paragraphIndex)
                .Select(row => row.Copy()));
            blockIndex = paragraphs.Count;
            paragraphs.Add(CreateDisplayBlock(string.Empty));
            paragraphs.AddRange(document.Paragraphs.Skip(lastCellIndex + 1).Select(row => row.Copy()));
            return new NotesDocument { Paragraphs = paragraphs };
        }

        if (source.Kind is NoteBlockKind.Code or NoteBlockKind.Divider or NoteBlockKind.DisplayMath)
        {
            paragraphs.Add(source.Copy());
            blockIndex = paragraphs.Count;
            paragraphs.Add(CreateDisplayBlock(string.Empty));
            paragraphs.AddRange(document.Paragraphs.Skip(paragraphIndex + 1).Select(row => row.Copy()));
            return new NotesDocument { Paragraphs = paragraphs };
        }

        var sourceLength = GetText(source).Length;
        var split = document.SplitParagraph(paragraphIndex, localOffset);
        var left = split.Paragraphs[paragraphIndex];
        var right = split.Paragraphs[paragraphIndex + 1];
        if (localOffset > 0) paragraphs.Add(left);
        blockIndex = paragraphs.Count;
        paragraphs.Add(CreateDisplayBlock(string.Empty));
        if (localOffset < sourceLength)
            paragraphs.Add(right);
        else if (paragraphIndex == document.Paragraphs.Count - 1)
            paragraphs.Add(new NoteParagraph());
        paragraphs.AddRange(document.Paragraphs.Skip(paragraphIndex + 1).Select(row => row.Copy()));
        return new NotesDocument { Paragraphs = paragraphs };
    }

    private static List<Formula> FindSameLineFormulas(string text, NoteTextStyle[] styles, NoteInlineKind[] kinds,
        (int Start, int Length) input, int paragraphStart)
    {
        var result = new List<Formula>();
        for (var index = 0; index < text.Length;)
        {
            if (text[index] != '$' || IsEscaped(text, index) || styles[index].HasFlag(NoteTextStyle.Code) ||
                kinds[index] != NoteInlineKind.Text)
            {
                index++;
                continue;
            }

            var runLength = CountDollars(text, index);
            if (runLength != 2)
            {
                index += runLength;
                continue;
            }

            var close = FindClosingDollars(text, index + runLength, styles, kinds);
            if (close < 0)
                break;

            var end = close + 2;
            if ((OverlapsInput(paragraphStart + index, 2, input) || OverlapsInput(paragraphStart + close, 2, input)) &&
                !styles.AsSpan(index, end - index).ToArray().Any(style => style.HasFlag(NoteTextStyle.Code)) &&
                !kinds.AsSpan(index, end - index).Contains(NoteInlineKind.InlineMath) &&
                !kinds.AsSpan(index, end - index).Contains(NoteInlineKind.Link))
                result.Add(new Formula(index, end));
            index = end;
        }

        return result;
    }

    private static int FindClosingDollars(string text, int start, NoteTextStyle[] styles, NoteInlineKind[] kinds)
    {
        for (var index = start; index < text.Length;)
        {
            if (text[index] is '\r' or '\n') return -1;
            if (text[index] != '$' || IsEscaped(text, index) || styles[index].HasFlag(NoteTextStyle.Code) ||
                kinds[index] != NoteInlineKind.Text)
            {
                index++;
                continue;
            }

            var runLength = CountDollars(text, index);
            if (runLength == 2) return index;
            index += runLength;
        }

        return -1;
    }

    private static int FindClosingDelimiterLine(IReadOnlyList<NoteParagraph> paragraphs, int start)
    {
        for (var index = start; index < paragraphs.Count; index++)
        {
            var row = paragraphs[index];
            if (row.Cell is not null || row.Kind is not NoteBlockKind.Paragraph || row.Kind == NoteBlockKind.Code)
                return -1;
            var text = GetText(row);
            if (IsDisplayDelimiterLine(text) && !HasCodeStyle(row, 0, text.Length)) return index;
            if (!IsBodyTextKind(row.Kind)) return -1;
        }
        return -1;
    }

    private static NoteParagraph CreateDisplayBlock(string source) => new()
    {
        Kind = NoteBlockKind.DisplayMath,
        Runs = source.Length == 0 ? [] : [new NoteTextRun(source)]
    };

    private static NoteParagraph CreateTextFragment(NoteParagraph source, int start, int end) =>
        CreateFragment(source, start, end) ?? new NoteParagraph { Kind = source.Kind, ListDepth = source.ListDepth };

    private static NoteParagraph? CreateFragment(NoteParagraph source, int start, int end)
    {
        if (end <= start) return null;
        return new NoteParagraph
        {
            Kind = source.Kind,
            ListDepth = source.ListDepth,
            IsChecked = source.Kind == NoteBlockKind.Checklist && source.IsChecked,
            Runs = SliceRuns(source, start, end)
        };
    }

    private static List<NoteTextRun> SliceRuns(NoteParagraph paragraph, int start, int end)
    {
        var result = new List<NoteTextRun>();
        var offset = 0;
        foreach (var run in paragraph.Runs)
        {
            var length = NormalizeLineBreaks(run.Text).Length;
            var from = Math.Clamp(start - offset, 0, length);
            var to = Math.Clamp(end - offset, from, length);
            if (to > from)
            {
                var rawStart = RawOffset(run.Text, from);
                var rawEnd = RawOffset(run.Text, to);
                var kind = run.Kind == NoteInlineKind.InlineMath && (from != 0 || to != length)
                    ? NoteInlineKind.Text
                    : run.Kind;
                AddRun(result, run with { Text = run.Text[rawStart..rawEnd], Kind = kind });
            }
            offset += length;
        }
        return result;
    }

    private static void AddRun(List<NoteTextRun> target, NoteTextRun value)
    {
        if (target.Count > 0 && target[^1].Style == value.Style && target[^1].Kind == value.Kind &&
            target[^1].LinkDestination == value.LinkDestination)
            target[^1] = target[^1] with { Text = target[^1].Text + value.Text };
        else target.Add(value);
    }

    private static void GetCharacterData(IReadOnlyList<NoteTextRun> runs, int length,
        out NoteTextStyle[] styles, out NoteInlineKind[] kinds)
    {
        styles = new NoteTextStyle[length];
        kinds = new NoteInlineKind[length];
        var offset = 0;
        foreach (var run in runs)
        {
            var count = NormalizeLineBreaks(run.Text).Length;
            for (var index = 0; index < count && offset < length; index++, offset++)
            {
                styles[offset] = run.Style;
                kinds[offset] = run.Kind;
            }
        }
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

    private static bool IsBodyTextKind(NoteBlockKind kind) => kind is NoteBlockKind.Paragraph or
        NoteBlockKind.Heading1 or NoteBlockKind.Heading2 or NoteBlockKind.Heading3 or
        NoteBlockKind.Heading4 or NoteBlockKind.Heading5 or NoteBlockKind.Heading6;

    private static bool IsDisplayDelimiterLine(string text) => text.Trim() == "$$";

    private static bool IsClosingFence(string text) =>
        text.Trim().Length == 3 && text.Trim().All(character => character == '\x60');

    private static int CountDollars(string text, int start)
    {
        var end = start + 1;
        while (end < text.Length && text[end] == '$') end++;
        return end - start;
    }

    private static bool IsEscaped(string text, int index)
    {
        var slashes = 0;
        for (var cursor = index - 1; cursor >= 0 && text[cursor] == '\\'; cursor--) slashes++;
        return slashes % 2 != 0;
    }

    private static bool OverlapsInput(int start, int length, (int Start, int Length) input) =>
        input.Length > 0 && start < input.Start + input.Length && start + length > input.Start;

    private static string GetText(NoteParagraph row) =>
        NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)));

    private static void ValidateRange(NotesDocument document, int start, int length)
    {
        var total = document.GetPlainText().Length;
        if (start < 0 || length < 0 || start > total - length)
            throw new ArgumentOutOfRangeException(nameof(start), "The text range is outside the note.");
    }
}
