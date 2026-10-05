using static DesktopPet.Core.NoteTextOffsets;
using System.Text.Json.Serialization;

namespace DesktopPet.Core;

[Flags]
public enum NoteTextStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikethrough = 8,
    Code = 16
}

public enum NoteBlockKind
{
    Paragraph,
    Bulleted,
    Numbered,
    Checklist,
    Code
}

public sealed record NoteTextRun(string Text, NoteTextStyle Style = NoteTextStyle.None);

public sealed class NoteParagraph
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NoteTableCell? Cell { get; set; }
    public NoteBlockKind Kind { get; set; } = NoteBlockKind.Paragraph;
    public bool IsChecked { get; set; }
    public List<NoteTextRun> Runs { get; set; } = [];

    internal NoteParagraph Copy() => new()
    {
        Cell = Cell,
        Kind = Kind,
        IsChecked = IsChecked,
        Runs = [.. Runs]
    };
}

public sealed class NotesDocument
{
    private const NoteTextStyle SupportedStyles = NoteTextStyle.Bold |
        NoteTextStyle.Italic |
        NoteTextStyle.Underline |
        NoteTextStyle.Strikethrough |
        NoteTextStyle.Code;

    public List<NoteParagraph> Paragraphs { get; set; } = [new()];

    public string GetPlainText() => string.Join("\r\n", Paragraphs.Select(paragraph =>
        NormalizeLineBreaks(string.Concat(paragraph.Runs.Select(run => run.Text)))));

    public NotesDocument Copy() => new() { Paragraphs = Paragraphs.Select(paragraph => paragraph.Copy()).ToList() };

    public static NotesDocument FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        return new NotesDocument
        {
            Paragraphs = lines.Select(line => new NoteParagraph
            {
                Runs = line.Length == 0 ? [] : [new NoteTextRun(line)]
            }).ToList()
        };
    }

    public NotesDocument SetStyle(int start, int length, NoteTextStyle style, bool enabled)
    {
        var plainTextLength = GetPlainText().Length;
        if (start < 0 || length < 0 || start > plainTextLength - length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The selected text range is outside the note.");
        }

        var styleBits = (int)style;
        if (style == NoteTextStyle.None || (style & ~SupportedStyles) != 0 || (styleBits & (styleBits - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(style), "Select one supported inline text style.");
        }

        if (length == 0)
        {
            return Copy();
        }

        var selectionEnd = start + length;
        var offset = 0;
        var paragraphs = new List<NoteParagraph>(Paragraphs.Count);
        for (var paragraphIndex = 0; paragraphIndex < Paragraphs.Count; paragraphIndex++)
        {
            var source = Paragraphs[paragraphIndex];
            var runs = new List<NoteTextRun>();
            foreach (var run in source.Runs)
            {
                var runStart = offset;
                var runLength = NormalizeLineBreaks(run.Text).Length;
                var runEnd = runStart + runLength;
                var selectedStart = RawOffset(run.Text, Math.Clamp(start - runStart, 0, runLength));
                var selectedEnd = RawOffset(run.Text, Math.Clamp(selectionEnd - runStart, 0, runLength));
                AddRange(runs, run.Text, 0, selectedStart, run.Style);
                if (selectedEnd > selectedStart)
                {
                    var selectedStyle = enabled ? run.Style | style : run.Style & ~style;
                    AddRange(runs, run.Text, selectedStart, selectedEnd, selectedStyle);
                }

                AddRange(runs, run.Text, selectedEnd, run.Text.Length, run.Style);
                offset = runEnd;
            }

            paragraphs.Add(new NoteParagraph { Cell = source.Cell, Kind = source.Kind, IsChecked = source.IsChecked, Runs = runs });
            if (paragraphIndex < Paragraphs.Count - 1)
            {
                offset += 2;
            }
        }

        return new NotesDocument { Paragraphs = paragraphs };
    }

    public NoteBlockKind? GetListKind(int start, int length)
    {
        var indexes = GetSelectedParagraphIndexes(start, length);
        if (indexes.Count == 0)
        {
            return null;
        }

        var kind = Paragraphs[indexes[0]].Kind;
        return indexes.All(index => Paragraphs[index].Kind == kind) ? kind : null;
    }

    public int GetParagraphIndexAt(int offset)
    {
        return GetParagraphPositionAt(offset).ParagraphIndex;
    }

    public (int ParagraphIndex, int OffsetInParagraph) GetParagraphPositionAt(int offset)
    {
        var textLength = GetPlainText().Length;
        if (offset < 0 || offset > textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var paragraphStart = 0;
        for (var index = 0; index < Paragraphs.Count; index++)
        {
            var paragraphLength = NormalizeLineBreaks(string.Concat(Paragraphs[index].Runs.Select(run => run.Text))).Length;
            var paragraphEnd = paragraphStart + paragraphLength;
            if (offset <= paragraphEnd || index == Paragraphs.Count - 1)
            {
                return (index, Math.Clamp(offset - paragraphStart, 0, paragraphLength));
            }

            var nextParagraphStart = paragraphEnd + 2;
            if (offset < nextParagraphStart)
            {
                return (index, paragraphLength);
            }

            paragraphStart = nextParagraphStart;
        }

        var lastIndex = Paragraphs.Count - 1;
        var lastLength = NormalizeLineBreaks(string.Concat(Paragraphs[lastIndex].Runs.Select(run => run.Text))).Length;
        return (lastIndex, lastLength);
    }

    public (int Start, int Length) GetParagraphRange(int paragraphIndex)
    {
        if (paragraphIndex < 0 || paragraphIndex >= Paragraphs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        var start = 0;
        for (var index = 0; index < paragraphIndex; index++)
        {
            start += NormalizeLineBreaks(string.Concat(Paragraphs[index].Runs.Select(run => run.Text))).Length + 2;
        }

        var length = NormalizeLineBreaks(string.Concat(Paragraphs[paragraphIndex].Runs.Select(run => run.Text))).Length;
        return (start, length);
    }

    public NotesDocument SetListKind(int start, int length, NoteBlockKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Select a supported paragraph or list kind.");
        }

        var indexes = GetSelectedParagraphIndexes(start, length);
        if (indexes.Count == 0)
        {
            return Copy();
        }

        var selected = indexes.ToHashSet();
        return new NotesDocument
        {
            Paragraphs = Paragraphs.Select((paragraph, index) =>
            {
                var copy = paragraph.Copy();
                if (selected.Contains(index))
                {
                    copy.Kind = kind;
                    if (kind != NoteBlockKind.Checklist)
                    {
                        copy.IsChecked = false;
                    }
                }

                return copy;
            }).ToList()
        };
    }

    public NotesDocument ToggleListKind(int start, int length, NoteBlockKind kind)
    {
        var indexes = GetSelectedParagraphIndexes(start, length);
        var targetKind = indexes.Count > 0 && indexes.All(index => Paragraphs[index].Kind == kind)
            ? NoteBlockKind.Paragraph
            : kind;
        return SetListKind(start, length, targetKind);
    }

    public NotesDocument EndEmptyListItem(int paragraphIndex)
    {
        if (paragraphIndex < 0 || paragraphIndex >= Paragraphs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        var current = Paragraphs[paragraphIndex];
        if (current.Kind == NoteBlockKind.Paragraph || current.Runs.Any(run => run.Text.Length > 0))
        {
            return Copy();
        }

        var paragraphs = Paragraphs.Select(paragraph => paragraph.Copy()).ToList();
        paragraphs[paragraphIndex].Kind = NoteBlockKind.Paragraph;
        paragraphs[paragraphIndex].IsChecked = false;
        return new NotesDocument { Paragraphs = paragraphs };
    }

    public NotesDocument SetChecklistChecked(int paragraphIndex, bool isChecked)
    {
        if (paragraphIndex < 0 || paragraphIndex >= Paragraphs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        if (Paragraphs[paragraphIndex].Kind != NoteBlockKind.Checklist)
        {
            throw new InvalidOperationException("Only checklist items have a checked state.");
        }

        var paragraphs = Paragraphs.Select(paragraph => paragraph.Copy()).ToList();
        paragraphs[paragraphIndex].IsChecked = isChecked;
        return new NotesDocument { Paragraphs = paragraphs };
    }

    public NotesDocument SplitParagraph(int paragraphIndex, int offset)
    {
        if (paragraphIndex < 0 || paragraphIndex >= Paragraphs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        var source = Paragraphs[paragraphIndex];
        var sourceLength = NormalizeLineBreaks(string.Concat(source.Runs.Select(run => run.Text))).Length;
        if (offset < 0 || offset > sourceLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var leftRuns = new List<NoteTextRun>();
        var rightRuns = new List<NoteTextRun>();
        var runOffset = 0;
        foreach (var run in source.Runs)
        {
            var runLength = NormalizeLineBreaks(run.Text).Length;
            var runEnd = runOffset + runLength;
            var splitOffset = RawOffset(run.Text, Math.Clamp(offset - runOffset, 0, runLength));
            AddRange(leftRuns, run.Text, 0, splitOffset, run.Style);
            AddRange(rightRuns, run.Text, splitOffset, run.Text.Length, run.Style);
            runOffset = runEnd;
        }

        var paragraphs = Paragraphs.Select(paragraph => paragraph.Copy()).ToList();
        var left = paragraphs[paragraphIndex];
        left.Runs = leftRuns;
        if (leftRuns.All(run => run.Text.Length == 0))
        {
            left.IsChecked = false;
        }

        var right = new NoteParagraph
        {
            Cell = source.Cell,
            Kind = source.Kind,
            IsChecked = false,
            Runs = rightRuns
        };
        paragraphs.Insert(paragraphIndex + 1, right);
        return new NotesDocument { Paragraphs = paragraphs };
    }

    private List<int> GetSelectedParagraphIndexes(int start, int length)
    {
        var plainTextLength = GetPlainText().Length;
        if (start < 0 || length < 0 || start > plainTextLength - length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The selected text range is outside the note.");
        }

        var indexes = new List<int>();
        var end = start + length;
        var offset = 0;
        for (var index = 0; index < Paragraphs.Count; index++)
        {
            var paragraph = Paragraphs[index];
            var paragraphStart = offset;
            var paragraphLength = NormalizeLineBreaks(string.Concat(paragraph.Runs.Select(run => run.Text))).Length;
            var paragraphEnd = paragraphStart + paragraphLength;
            var selected = length == 0
                ? start >= paragraphStart && (start <= paragraphEnd || index == Paragraphs.Count - 1)
                : paragraphLength == 0
                    ? start <= paragraphStart && end >= paragraphStart
                    : start < paragraphEnd && end > paragraphStart;
            if (selected)
            {
                indexes.Add(index);
            }

            offset = paragraphEnd + (index < Paragraphs.Count - 1 ? 2 : 0);
        }

        return indexes;
    }

    private static void AddRange(List<NoteTextRun> target, string text, int start, int end, NoteTextStyle style)
    {
        if (end <= start)
        {
            return;
        }

        var value = text[start..end];
        if (target.Count > 0 && target[^1].Style == style)
        {
            target[^1] = target[^1] with { Text = target[^1].Text + value };
        }
        else
        {
            target.Add(new NoteTextRun(value, style));
        }
    }
}
