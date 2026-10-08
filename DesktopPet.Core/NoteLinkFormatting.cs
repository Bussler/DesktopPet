using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteLinkFormatting
{
    private readonly record struct Link(int Start, int End, int LabelStart, int LabelEnd, string Destination,
        bool IsMarkdown);

    public static bool IsValidDestination(string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination) || destination != destination.Trim() ||
            destination.Any(char.IsWhiteSpace) ||
            !Uri.TryCreate(destination, UriKind.Absolute, out var uri))
            return false;

        for (var index = destination.IndexOf('%'); index >= 0; index = destination.IndexOf('%', index + 3))
        {
            if (index + 2 >= destination.Length || !Uri.IsHexDigit(destination[index + 1]) ||
                !Uri.IsHexDigit(destination[index + 2]))
                return false;
        }

        return (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) &&
               !string.IsNullOrWhiteSpace(uri.Host);
    }

    public static (int Start, int Length)? FindPlainUrlEndingAt(NotesDocument document, int offset)
    {
        ArgumentNullException.ThrowIfNull(document);
        var (paragraphIndex, localOffset) = document.GetParagraphPositionAt(offset);
        var paragraph = document.Paragraphs[paragraphIndex];
        if (paragraph.Kind is NoteBlockKind.Code or NoteBlockKind.Divider or NoteBlockKind.DisplayMath)
            return null;

        var text = NormalizeLineBreaks(string.Concat(paragraph.Runs.Select(run => run.Text)));
        if (localOffset <= 0 || localOffset > text.Length)
            return null;

        GetCharacterData(paragraph.Runs, text.Length, out var styles, out var kinds, out var destinations);
        var mathSource = GetLinkMathSource(text, styles, kinds);
        var tokenStart = localOffset;
        while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1])) tokenStart--;
        for (var start = tokenStart; start < localOffset; start++)
        {
            if (!IsUrlStart(text, start) || !HasUrlBoundaryBefore(text, start) || IsEscaped(text, start)) continue;
            var candidateEnd = TrimUrlEnding(text, start, localOffset);
            if (candidateEnd <= start || !IsRawSource(start) || mathSource[start] ||
                !Enumerable.Range(start, candidateEnd - start).All(IsRawSource) ||
                !IsValidDestination(text[start..candidateEnd])) continue;
            var paragraphStart = document.GetParagraphRange(paragraphIndex).Start;
            return (paragraphStart + start, candidateEnd - start);
        }

        return null;

        bool IsRawSource(int position) => position < text.Length &&
            !styles[position].HasFlag(NoteTextStyle.Code) && kinds[position] == NoteInlineKind.Text &&
            destinations[position] is null;
    }

    public static NotesDocument Apply(NotesDocument document, (int Start, int Length) input,
        ref int selectionStart, ref int selectionLength, bool pasted = false,
        (int Start, int Length)? forcePlainUrl = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateRange(document, input.Start, input.Length);

        var result = new NotesDocument { Paragraphs = [] };
        var removed = new List<(int Start, int Length)>();
        var paragraphStart = 0;
        var insideFence = false;
        var insideDisplayMath = false;
        var changed = false;
        foreach (var row in document.Paragraphs)
        {
            var text = NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)));
            if (row.Kind is NoteBlockKind.Code or NoteBlockKind.Divider or NoteBlockKind.DisplayMath)
            {
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }

            if (insideFence && row.Cell is null)
            {
                result.Paragraphs.Add(row.Copy());
                if (IsClosingFence(text)) insideFence = false;
                paragraphStart += text.Length + 2;
                continue;
            }
            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph && NoteCodeFormatting.IsOpeningFence(text))
            {
                insideFence = true;
                result.Paragraphs.Add(row.Copy());
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

            GetCharacterData(row.Runs, text.Length, out var styles, out var kinds, out var destinations);
            var mathSource = GetLinkMathSource(text, styles, kinds);
            var links = FindLinks(text, styles, kinds, destinations, mathSource, input, paragraphStart, pasted,
                forcePlainUrl);
            if (links.Count == 0)
            {
                result.Paragraphs.Add(row.Copy());
                paragraphStart += text.Length + 2;
                continue;
            }

            changed = true;
            var byStart = links.ToDictionary(link => link.Start);
            var runs = new List<NoteTextRun>();
            for (var index = 0; index < text.Length;)
            {
                if (byStart.TryGetValue(index, out var link))
                {
                    if (link.IsMarkdown)
                    {
                        AppendLinkedRange(runs, row.Runs, link.LabelStart, link.LabelEnd, link.Destination);
                        removed.Add((paragraphStart + link.Start, 1));
                        removed.Add((paragraphStart + link.LabelEnd, link.End - link.LabelEnd));
                    }
                    else
                    {
                        AppendLinkedRange(runs, row.Runs, link.Start, link.End, link.Destination);
                    }
                    index = link.End;
                    continue;
                }

                var start = index++;
                while (index < text.Length && !byStart.ContainsKey(index) &&
                       styles[index] == styles[start] && kinds[index] == kinds[start] &&
                       destinations[index] == destinations[start])
                    index++;
                AddRun(runs, new NoteTextRun(text[start..index], styles[start], kinds[start], destinations[start]));
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

        if (!changed) return document;
        var originalEnd = selectionStart + selectionLength;
        var mappedStart = MapOffset(selectionStart);
        var mappedEnd = MapOffset(originalEnd);
        selectionStart = mappedStart;
        selectionLength = Math.Max(0, mappedEnd - mappedStart);
        return result;

        int MapOffset(int position) => position - removed.Sum(range => Math.Clamp(position - range.Start, 0, range.Length));
    }

    public static NotesDocument UpdateLink(NotesDocument document, int start, int length, string label, string destination)
    {
        if (string.IsNullOrEmpty(label) || label.Contains('\r') || label.Contains('\n'))
            throw new ArgumentException("A link label must contain one line of visible text.", nameof(label));
        if (!IsValidDestination(destination))
            throw new ArgumentException("Enter an absolute HTTP or HTTPS address.", nameof(destination));
        return RewriteLink(document, start, length, label, destination);
    }

    public static NotesDocument RemoveLink(NotesDocument document, int start, int length) =>
        RewriteLink(document, start, length, label: null, destination: null);

    private static NotesDocument RewriteLink(NotesDocument document, int start, int length, string? label,
        string? destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateRange(document, start, length);
        if (length == 0) throw new ArgumentOutOfRangeException(nameof(length));
        var (first, localStart) = document.GetParagraphPositionAt(start);
        var (last, localEnd) = document.GetParagraphPositionAt(start + length);
        if (first != last) throw new InvalidOperationException("A link edit must stay within one paragraph.");

        var source = document.Paragraphs[first];
        var selected = GetSegments(source.Runs, localStart, localEnd);
        if (selected.Count == 0 || selected.Any(segment => segment.Run.Kind != NoteInlineKind.Link ||
                segment.Run.LinkDestination != selected[0].Run.LinkDestination))
            throw new InvalidOperationException("Select one complete link to edit or remove it.");

        var style = selected[0].Run.Style;
        var runs = new List<NoteTextRun>();
        AppendRange(runs, source.Runs, 0, localStart);
        if (label is null)
            AppendRange(runs, source.Runs, localStart, localEnd, kindOverride: NoteInlineKind.Text);
        else
            AddRun(runs, new NoteTextRun(label, style, NoteInlineKind.Link, destination));
        AppendRange(runs, source.Runs, localEnd, GetTextLength(source));

        var paragraphs = document.Paragraphs.Select(row => row.Copy()).ToList();
        paragraphs[first].Runs = runs;
        return new NotesDocument { Paragraphs = paragraphs };
    }

    private static List<Link> FindLinks(string text, NoteTextStyle[] styles, NoteInlineKind[] kinds,
        string?[] destinations, bool[] mathSource, (int Start, int Length) input, int paragraphStart, bool pasted,
        (int Start, int Length)? forcePlainUrl)
    {
        var links = new List<Link>();
        for (var index = 0; index < text.Length;)
        {
            if (text[index] == '[' && IsEscaped(text, index))
            {
                var escapedLabelEnd = FindMarkdownLabelEnd(text, index + 1, styles, kinds);
                if (escapedLabelEnd >= 0 && escapedLabelEnd + 1 < text.Length && text[escapedLabelEnd + 1] == '(')
                {
                    var escapedClose = FindMarkdownDestinationEnd(text, escapedLabelEnd + 2, styles, kinds);
                    if (escapedClose < 0) break;
                    index = escapedClose + 1;
                    continue;
                }
            }

            if (text[index] == '[' && IsPlainSource(index))
            {
                var labelEnd = FindMarkdownLabelEnd(text, index + 1, styles, kinds);
                if (labelEnd >= 0 && labelEnd + 1 < text.Length && text[labelEnd + 1] == '(')
                {
                    var close = FindMarkdownDestinationEnd(text, labelEnd + 2, styles, kinds);
                    if (close < 0) break;
                    var destination = text[(labelEnd + 2)..close];
                    if (IsValidDestination(destination) && labelEnd > index + 1 &&
                        IsEligibleSpan(index, labelEnd + 1) && IsRawSourceSpan(labelEnd + 2, close) &&
                        OverlapsInput(paragraphStart + close, 1, input))
                    {
                        links.Add(new Link(index, close + 1, index + 1, labelEnd, destination, IsMarkdown: true));
                    }
                    index = close + 1;
                    continue;
                }
            }

            if (!IsUrlStart(text, index) || !HasUrlBoundaryBefore(text, index) || IsEscaped(text, index) || !IsEligible(index))
            {
                index++;
                continue;
            }

            var rawEnd = index;
            while (rawEnd < text.Length && !char.IsWhiteSpace(text[rawEnd]) && IsRawSource(rawEnd)) rawEnd++;
            var end = TrimUrlEnding(text, index, rawEnd);
            if (end <= index)
            {
                index++;
                continue;
            }

            var globalStart = paragraphStart + index;
            var globalEnd = paragraphStart + end;
            var pastedCompletion = pasted && OverlapsInput(globalStart, end - index, input);
            var forcedCompletion = forcePlainUrl is { } forced && forced.Start == globalStart && forced.Start + forced.Length == globalEnd;
            if (IsValidDestination(text[index..end]) && (pastedCompletion || forcedCompletion))
                links.Add(new Link(index, end, index, end, text[index..end], IsMarkdown: false));
            index = rawEnd;

            bool IsEligible(int position) =>
                IsRawSource(position) && !mathSource[position];
            bool IsRawSource(int position) =>
                position < text.Length && !styles[position].HasFlag(NoteTextStyle.Code) &&
                kinds[position] == NoteInlineKind.Text && destinations[position] is null;
            bool IsPlainSource(int position) => IsEligible(position) && !IsEscaped(text, position);
            bool IsEligibleSpan(int start, int end) => Enumerable.Range(start, end - start).All(IsEligible);
            bool IsRawSourceSpan(int start, int end) => Enumerable.Range(start, end - start).All(IsRawSource);
        }
        return links;
    }

    private static bool[] GetLinkMathSource(string text, NoteTextStyle[] styles, NoteInlineKind[] kinds)
    {
        var mathText = text.ToCharArray();
        for (var index = 0; index < text.Length;)
        {
            if (!IsUrlStart(text, index) || !HasUrlBoundaryBefore(text, index) || IsEscaped(text, index))
            {
                index++;
                continue;
            }

            var rawEnd = index;
            while (rawEnd < text.Length && !char.IsWhiteSpace(text[rawEnd])) rawEnd++;
            var end = TrimUrlEnding(text, index, rawEnd);
            if (end > index && IsValidDestination(text[index..end]))
            {
                for (var position = index; position < end; position++)
                    if (mathText[position] == '$') mathText[position] = 'x';
                index = end;
            }
            else index++;
        }

        return NoteCodeFormatting.GetMathSource(new string(mathText), styles, kinds);
    }

    private static int FindMarkdownLabelEnd(string text, int start, NoteTextStyle[] styles, NoteInlineKind[] kinds)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] is '\r' or '\n' || styles[index].HasFlag(NoteTextStyle.Code) ||
                kinds[index] != NoteInlineKind.Text) return -1;
            if (text[index] == ']' && !IsEscaped(text, index)) return index;
        }
        return -1;
    }

    private static int FindMarkdownDestinationEnd(string text, int start, NoteTextStyle[] styles,
        NoteInlineKind[] kinds)
    {
        var depth = 1;
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] is '\r' or '\n' || styles[index].HasFlag(NoteTextStyle.Code) ||
                kinds[index] != NoteInlineKind.Text) return -1;
            if (IsEscaped(text, index)) continue;
            if (text[index] == '(') depth++;
            else if (text[index] == ')' && --depth == 0) return index;
        }
        return -1;
    }

    private static bool IsUrlStart(string text, int index) =>
        Matches(text, index, "http://") || Matches(text, index, "https://");

    private static bool Matches(string text, int index, string value) =>
        index + value.Length <= text.Length && text.AsSpan(index, value.Length).Equals(value,
            StringComparison.OrdinalIgnoreCase);

    private static bool HasUrlBoundaryBefore(string text, int index) => index == 0 ||
        !(char.IsLetterOrDigit(text[index - 1]) || text[index - 1] is '_' or '@');

    private static int TrimUrlEnding(string text, int start, int end)
    {
        while (end > start)
        {
            var last = text[end - 1];
            if (IsSentencePunctuation(last) || last is '\'' or '"')
            {
                end--;
                continue;
            }

            var opening = last switch { ')' => '(', ']' => '[', '}' => '{', _ => '\0' };
            if (opening != '\0' && Count(text, start, end, last) > Count(text, start, end, opening))
            {
                end--;
                continue;
            }
            break;
        }
        return end;
    }

    private static bool IsSentencePunctuation(char value) => value is '.' or ',' or ';' or ':' or '!' or '?';

    private static int Count(string text, int start, int end, char value)
    {
        var count = 0;
        for (var index = start; index < end; index++)
            if (text[index] == value) count++;
        return count;
    }

    private static bool IsEscaped(string text, int index)
    {
        var slashes = 0;
        for (var cursor = index - 1; cursor >= 0 && text[cursor] == '\\'; cursor--) slashes++;
        return slashes % 2 != 0;
    }

    private static bool IsClosingFence(string text) =>
        text.Trim().Length == 3 && text.Trim().All(character => character == '\x60');

    private static void AppendLinkedRange(List<NoteTextRun> target, IReadOnlyList<NoteTextRun> source,
        int start, int end, string destination) =>
        AppendRange(target, source, start, end, NoteInlineKind.Link, destination);

    private static void AppendRange(List<NoteTextRun> target, IReadOnlyList<NoteTextRun> source, int start, int end,
        NoteInlineKind? kindOverride = null, string? destinationOverride = null)
    {
        if (end <= start) return;
        var offset = 0;
        foreach (var run in source)
        {
            var runLength = NormalizeLineBreaks(run.Text).Length;
            var from = Math.Clamp(start - offset, 0, runLength);
            var to = Math.Clamp(end - offset, from, runLength);
            if (to > from)
            {
                var rawStart = RawOffset(run.Text, from);
                var rawEnd = RawOffset(run.Text, to);
                AddRun(target, new NoteTextRun(run.Text[rawStart..rawEnd], run.Style,
                    kindOverride ?? run.Kind, kindOverride == NoteInlineKind.Link
                        ? destinationOverride
                        : kindOverride == NoteInlineKind.Text ? null : run.LinkDestination));
            }
            offset += runLength;
            if (offset >= end) break;
        }
    }

    private static void AddRun(List<NoteTextRun> runs, NoteTextRun value)
    {
        if (value.Text.Length == 0) return;
        if (runs.Count > 0 && runs[^1].Style == value.Style && runs[^1].Kind == value.Kind &&
            runs[^1].LinkDestination == value.LinkDestination)
            runs[^1] = runs[^1] with { Text = runs[^1].Text + value.Text };
        else
            runs.Add(value);
    }

    private static List<(NoteTextRun Run, int Start, int End)> GetSegments(IReadOnlyList<NoteTextRun> runs,
        int start, int end)
    {
        var selected = new List<(NoteTextRun Run, int Start, int End)>();
        var offset = 0;
        foreach (var run in runs)
        {
            var runEnd = offset + NormalizeLineBreaks(run.Text).Length;
            var from = Math.Max(start, offset);
            var to = Math.Min(end, runEnd);
            if (to > from) selected.Add((run, from, to));
            offset = runEnd;
        }
        return selected;
    }

    private static int GetTextLength(NoteParagraph paragraph) =>
        NormalizeLineBreaks(string.Concat(paragraph.Runs.Select(run => run.Text))).Length;

    private static void GetCharacterData(IReadOnlyList<NoteTextRun> runs, int length,
        out NoteTextStyle[] styles, out NoteInlineKind[] kinds, out string?[] destinations)
    {
        styles = new NoteTextStyle[length];
        kinds = new NoteInlineKind[length];
        destinations = new string?[length];
        var offset = 0;
        foreach (var run in runs)
        {
            var count = NormalizeLineBreaks(run.Text).Length;
            for (var index = 0; index < count && offset < length; index++, offset++)
            {
                styles[offset] = run.Style;
                kinds[offset] = run.Kind;
                destinations[offset] = run.LinkDestination;
            }
        }
    }

    private static bool OverlapsInput(int start, int length, (int Start, int Length) input) =>
        input.Length > 0 && start < input.Start + input.Length && start + length > input.Start;

    private static void ValidateRange(NotesDocument document, int start, int length)
    {
        var total = document.GetPlainText().Length;
        if (start < 0 || length < 0 || start > total - length)
            throw new ArgumentOutOfRangeException(nameof(start), "The text range is outside the note.");
    }
}
