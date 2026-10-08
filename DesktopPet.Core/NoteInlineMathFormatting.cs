using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteInlineMathFormatting
{
    public static NotesDocument Apply(NotesDocument document, (int Start, int Length) input,
        ref int selectionStart, ref int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(document);
        var documentLength = document.GetPlainText().Length;
        if (input.Start < 0 || input.Length < 0 || input.Start > documentLength - input.Length)
            throw new ArgumentOutOfRangeException(nameof(input), "The new input range is outside the note.");

        var result = new NotesDocument { Paragraphs = [] };
        var paragraphStart = 0;
        var insideFence = false;
        var insideDisplayMath = false;
        var changed = false;
        foreach (var row in document.Paragraphs)
        {
            var text = NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)));
            if (row.Kind is NoteBlockKind.Code or NoteBlockKind.DisplayMath)
            {
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }
            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph &&
                !insideFence && NoteCodeFormatting.IsOpeningFence(text))
            {
                insideFence = true;
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }
            if (insideFence && row.Cell is null)
            {
                result.Paragraphs.Add(row.Copy());
                if (row.Kind == NoteBlockKind.Paragraph && IsClosingFence(text)) insideFence = false;
                paragraphStart += text.Length + 2;
                continue;
            }
            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph &&
                !insideDisplayMath && text.Trim() == "$$")
            {
                insideDisplayMath = true;
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }
            if (insideDisplayMath && row.Cell is null)
            {
                result.Paragraphs.Add(row.Copy());
                if (row.Kind == NoteBlockKind.Paragraph && text.Trim() == "$$") insideDisplayMath = false;
                paragraphStart += text.Length + 2;
                continue;
            }
            if (row.Kind == NoteBlockKind.Divider)
            {
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }

            GetCharacterData(row.Runs, text.Length, out var styles, out var kinds, out var destinations);
            var code = styles.Select(style => style.HasFlag(NoteTextStyle.Code)).ToArray();
            var protectedSource = kinds.Select(kind => kind != NoteInlineKind.Text).ToArray();
            var formulas = FindFormulas(text, code, protectedSource, input, paragraphStart);
            if (formulas.Count == 0)
            {
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }

            changed = true;
            var formulaAt = formulas.ToDictionary(formula => formula.Start);
            var runs = new List<NoteTextRun>();
            for (var index = 0; index < text.Length;)
            {
                if (formulaAt.TryGetValue(index, out var formula))
                {
                    AddRun(runs, text[formula.Start..formula.End], styles[index], NoteInlineKind.InlineMath);
                    index = formula.End;
                    continue;
                }

                var start = index++;
                while (index < text.Length && !formulaAt.ContainsKey(index) &&
                       styles[index] == styles[start] && kinds[index] == kinds[start] &&
                       destinations[index] == destinations[start])
                    index++;
                AddRun(runs, text[start..index], styles[start], kinds[start], destinations[start]);
            }

            result.Paragraphs.Add(new NoteParagraph
            {
                Cell = row.Cell,
                Kind = row.Kind,
                ListDepth = row.ListDepth,
                IsChecked = row.IsChecked,
                Runs = runs
            });
            paragraphStart += text.Length + 2;
        }

        return changed ? result : document;
    }

    private static List<(int Start, int End)> FindFormulas(string text, bool[] code, bool[] protectedSource,
        (int Start, int Length) input, int paragraphStart)
    {
        var formulas = new List<(int Start, int End)>();
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

            var closing = FindClosing(text, index + delimiterLength, delimiterLength, code, protectedSource);
            if (closing < 0)
            {
                Array.Fill(protectedSource, true, index, text.Length - index);
                break;
            }

            var end = closing + delimiterLength;
            if (delimiterLength == 1 && !OverlapsInput(paragraphStart + index, input) &&
                OverlapsInput(paragraphStart + closing, input) &&
                HasLaterInputDollar(text, end, code, protectedSource, input, paragraphStart))
            {
                // Do not let an older unfinished opener consume the opening dollar of a newly pasted formula.
                index = closing;
                continue;
            }
            if (delimiterLength == 1 && closing > index + 1 &&
                !code.AsSpan(index, end - index).Contains(true) &&
                !protectedSource.AsSpan(index, end - index).Contains(true) &&
                (OverlapsInput(paragraphStart + index, input) || OverlapsInput(paragraphStart + closing, input)))
            {
                formulas.Add((index, end));
            }
            index = end;
        }

        return formulas;
    }

    private static bool HasLaterInputDollar(string text, int start, bool[] code, bool[] protectedSource,
        (int Start, int Length) input, int paragraphStart)
    {
        for (var index = start; index < text.Length;)
        {
            if (text[index] != '$' || code[index] || protectedSource[index] || IsEscaped(text, index))
            {
                index++;
                continue;
            }

            var runLength = CountDollarRun(text, index);
            if (runLength == 1 && OverlapsInput(paragraphStart + index, input)) return true;
            index += runLength;
        }
        return false;
    }

    private static int FindClosing(string text, int start, int delimiterLength, bool[] code, bool[] protectedSource)
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

    private static void GetCharacterData(IReadOnlyList<NoteTextRun> runs, int length,
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

    private static void AddRun(List<NoteTextRun> runs, string text, NoteTextStyle style, NoteInlineKind kind,
        string? destination = null)
    {
        if (text.Length == 0) return;
        if (runs.Count > 0 && runs[^1].Style == style && runs[^1].Kind == kind &&
            runs[^1].LinkDestination == destination)
            runs[^1] = runs[^1] with { Text = runs[^1].Text + text };
        else
            runs.Add(new NoteTextRun(text, style, kind, destination));
    }

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

    private static bool OverlapsInput(int position, (int Start, int Length) input) =>
        input.Length > 0 && position >= input.Start && position < input.Start + input.Length;
}
