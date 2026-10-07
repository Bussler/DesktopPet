using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteCodeFormatting
{
    // Offsets use the same CRLF-normalized text contract as the editor and edit history.
    // Live cell editing supplies the inserted range; source parsing formats all markers by default.
    public static NotesDocument Apply(NotesDocument document, ref int selectionStart, ref int selectionLength,
        (int Start, int Length)? cellCodeInput = null)
    {
        var paragraphs = document.Paragraphs;
        var starts = new int[paragraphs.Count];
        var texts = paragraphs.Select(row => NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)))).ToArray();
        for (var i = 1; i < starts.Length; i++)
            starts[i] = starts[i - 1] + texts[i - 1].Length + 2;

        var removed = new List<(int Start, int Length)>();
        var result = new NotesDocument { Paragraphs = [] };
        var pendingTable = false;
        var exitCodeBlock = false;
        var insideDisplayMath = false;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var row = paragraphs[i];
            if (NoteTableMarkdown.IsTableStart(document, i)) pendingTable = true;
            if (pendingTable && row.Cell is null && row.Kind == NoteBlockKind.Paragraph && texts[i].Contains('|'))
            {
                // Keep inline markers in Markdown source until the complete table is rendered.
                result.Paragraphs.Add(row.Copy());
                continue;
            }
            pendingTable = false;
            if (row.Kind is NoteBlockKind.Code or NoteBlockKind.DisplayMath)
            {
                result.Paragraphs.Add(row.Copy());
                continue;
            }
            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph &&
                !insideDisplayMath && texts[i].Trim() == "$$")
            {
                insideDisplayMath = true;
                result.Paragraphs.Add(row.Copy());
                continue;
            }
            if (insideDisplayMath && row.Cell is null)
            {
                result.Paragraphs.Add(row.Copy());
                if (row.Kind == NoteBlockKind.Paragraph && texts[i].Trim() == "$$") insideDisplayMath = false;
                continue;
            }

            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph && IsOpeningFence(texts[i]))
            {
                var end = i + 1;
                while (end < paragraphs.Count &&
                       !(paragraphs[end].Cell is null && paragraphs[end].Kind == NoteBlockKind.Paragraph && texts[end].Trim() == "```"))
                    end++;
                if (end == paragraphs.Count || paragraphs.Skip(i + 1).Take(end - i - 1).Any(paragraph => paragraph.Cell is not null))
                {
                    // An unfinished fence is literal text; do not consume backticks inside its body.
                    result.Paragraphs.AddRange(paragraphs.Skip(i).Select(item => item.Copy()));
                    break;
                }

                var code = new NoteParagraph { Kind = NoteBlockKind.Code };
                for (var body = i + 1; body < end; body++)
                {
                    if (body > i + 1) code.Runs.Add(new NoteTextRun("\r\n"));
                    code.Runs.AddRange(paragraphs[body].Runs.Select(run => run with
                    {
                        Text = NormalizeLineBreaks(run.Text),
                        Kind = NoteInlineKind.Text,
                        LinkDestination = null
                    }));
                }
                result.Paragraphs.Add(code);
                exitCodeBlock |= selectionLength == 0 && selectionStart >= starts[end] &&
                                selectionStart <= starts[end] + texts[end].Length;
                if (end == i + 1)
                    removed.Add((starts[i], texts[i].Length + 2 + texts[end].Length));
                else
                {
                    removed.Add((starts[i], texts[i].Length + 2));
                    removed.Add((starts[end] - 2, texts[end].Length + 2));
                }
                i = end;
                continue;
            }

            var text = texts[i];
            var styles = row.Runs.SelectMany(run => Enumerable.Repeat(run.Style, NormalizeLineBreaks(run.Text).Length)).ToArray();
            var kinds = row.Runs.SelectMany(run => Enumerable.Repeat(run.Kind, NormalizeLineBreaks(run.Text).Length)).ToArray();
            var destinations = row.Runs.SelectMany(run => Enumerable.Repeat(run.LinkDestination, NormalizeLineBreaks(run.Text).Length)).ToArray();
            var mathSource = GetMathSource(text, styles, kinds);
            var omit = new bool[text.Length];
            var opening = -1;
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] is '\r' or '\n' || styles[index].HasFlag(NoteTextStyle.Code) ||
                    kinds[index] != NoteInlineKind.Text || mathSource[index])
                {
                    opening = -1;
                    continue;
                }
                if (text[index] != '`') continue;
                if ((index > 0 && text[index - 1] == '`') || (index + 1 < text.Length && text[index + 1] == '`'))
                {
                    opening = -1;
                    continue;
                }
                var slashes = 0;
                for (var back = index - 1; back >= 0 && text[back] == '\\'; back--) slashes++;
                if (slashes % 2 != 0) continue;
                if (opening < 0) opening = index;
                else
                {
                    // Existing literal cell text (for example after Undo or Markdown decoding) stays literal.
                    if (row.Cell is not null && cellCodeInput is { } input &&
                        (starts[i] + index < input.Start || starts[i] + index >= input.Start + input.Length))
                    {
                        opening = -1;
                        continue;
                    }
                    omit[opening] = omit[index] = true;
                    removed.Add((starts[i] + opening, 1));
                    removed.Add((starts[i] + index, 1));
                    for (var code = opening + 1; code < index; code++) styles[code] |= NoteTextStyle.Code;
                    opening = -1;
                }
            }

            var formatted = new NoteParagraph { Cell = row.Cell, Kind = row.Kind, IsChecked = row.IsChecked };
            for (var index = 0; index < text.Length;)
            {
                if (omit[index]) { index++; continue; }
                var start = index++;
                while (index < text.Length && !omit[index] && styles[index] == styles[start] &&
                       kinds[index] == kinds[start] && destinations[index] == destinations[start]) index++;
                formatted.Runs.Add(new NoteTextRun(text[start..index], styles[start], kinds[start], destinations[start]));
            }
            result.Paragraphs.Add(formatted);
        }

        if (removed.Count == 0) return document;
        var endOffset = MapOffset(selectionStart + selectionLength);
        selectionStart = MapOffset(selectionStart);
        selectionLength = endOffset - selectionStart;
        if (exitCodeBlock)
        {
            var index = result.GetParagraphIndexAt(selectionStart);
            if (index + 1 == result.Paragraphs.Count || result.Paragraphs[index + 1].Cell is not null ||
                result.Paragraphs[index + 1].Kind != NoteBlockKind.Paragraph)
                result.Paragraphs.Insert(index + 1, new NoteParagraph());
            selectionStart = result.GetParagraphRange(index + 1).Start;
        }
        return result;

        int MapOffset(int offset) => offset - removed.Sum(range => Math.Clamp(offset - range.Start, 0, range.Length));
    }

    internal static bool[] GetMathSource(string text, NoteTextStyle[] styles, NoteInlineKind[] kinds)
    {
        var protectedSource = kinds.Select(kind => kind != NoteInlineKind.Text).ToArray();
        var code = styles.Select(style => style.HasFlag(NoteTextStyle.Code)).ToArray();
        for (var index = 0; index < text.Length;)
        {
            if (text[index] != '$' || code[index] || protectedSource[index] || IsEscaped(text, index))
            {
                index++;
                continue;
            }

            var delimiterLength = CountDollarRun(text, index);
            if (delimiterLength is not (1 or 2))
            {
                index += delimiterLength;
                continue;
            }

            var closing = FindClosingDollar(text, index + delimiterLength, delimiterLength, code, protectedSource);
            if (closing < 0)
            {
                Array.Fill(protectedSource, true, index, text.Length - index);
                break;
            }

            var end = closing + delimiterLength;
            Array.Fill(protectedSource, true, index, end - index);
            index = end;
        }
        return protectedSource;
    }

    private static int FindClosingDollar(string text, int start, int delimiterLength, bool[] code, bool[] protectedSource)
    {
        for (var index = start; index < text.Length;)
        {
            if (text[index] is '\r' or '\n') return -1;
            if (text[index] != '$' || code[index] || protectedSource[index] || IsEscaped(text, index))
            {
                index++;
                continue;
            }

            var runLength = CountDollarRun(text, index);
            if (runLength == delimiterLength) return index;
            index += runLength;
        }
        return -1;
    }

    private static int CountDollarRun(string text, int start)
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

    internal static bool IsOpeningFence(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith("```", StringComparison.Ordinal) &&
               trimmed.AsSpan(3).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_+.#-".AsSpan()) < 0;
    }
}
