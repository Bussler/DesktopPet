using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteTableMarkdown
{
    internal static bool IsTableStart(NotesDocument document, int index)
    {
        if (index + 1 >= document.Paragraphs.Count || document.Paragraphs[index].Cell is not null ||
            document.Paragraphs[index + 1].Cell is not null || document.Paragraphs[index].Kind != NoteBlockKind.Paragraph ||
            document.Paragraphs[index + 1].Kind != NoteBlockKind.Paragraph) return false;
        var header = SplitRow(Text(document.Paragraphs[index]));
        var separator = SplitRow(Text(document.Paragraphs[index + 1]));
        return header.Length > 0 && header.Length == separator.Length && separator.All(cell => Regex.IsMatch(cell, "^:?-{3,}:?$"));
    }

    public static NotesDocument Apply(NotesDocument document, bool pasted, ref int selectionStart, ref int selectionLength)
    {
        var result = new NotesDocument { Paragraphs = [] };
        var replacements = new List<(int Start, int OldLength, int NewLength)>();
        var rows = document.Paragraphs;
        var fenced = false;
        var offset = 0;
        for (var i = 0; i < rows.Count;)
        {
            var text = Text(rows[i]);
            if (rows[i].Cell is null && rows[i].Kind == NoteBlockKind.Paragraph)
                fenced = fenced ? text.Trim() != "```" : NoteCodeFormatting.IsOpeningFence(text);
            var header = SplitRow(text);
            var separator = i + 1 < rows.Count ? SplitRow(Text(rows[i + 1])) : [];
            var eligible = !fenced && IsTableStart(document, i);
            if (eligible)
            {
                var end = i + 2;
                var sourceRows = new List<string[]> { header };
                var valid = true;
                while (end < rows.Count && rows[end].Cell is null && rows[end].Kind == NoteBlockKind.Paragraph && SplitRow(Text(rows[end])) is { Length: > 0 } body)
                {
                    if (body.Length != header.Length) valid = false;
                    sourceRows.Add(body);
                    end++;
                }
                var finished = end < rows.Count && rows[end].Cell is null && Text(rows[end]).Length == 0;
                if (valid && (pasted || finished))
                {
                    var id = Guid.NewGuid();
                    var table = new List<NoteParagraph>();
                    for (var row = 0; row < sourceRows.Count; row++)
                        for (var column = 0; column < header.Length; column++)
                        {
                            var alignment = separator[column].EndsWith(':')
                                ? separator[column].StartsWith(':') ? NoteCellAlignment.Center : NoteCellAlignment.Right
                                : NoteCellAlignment.Left;
                            table.AddRange(ParseCell(sourceRows[row][column], new(id, row, column, alignment)));
                        }
                    var oldLength = rows.Skip(i).Take(end - i).Sum(row => Text(row).Length + 2) - 2;
                    var newLength = new NotesDocument { Paragraphs = table }.GetPlainText().Length;
                    replacements.Add((offset, oldLength, newLength));
                    result.Paragraphs.AddRange(table);
                    offset += oldLength + 2;
                    i = end;
                    continue;
                }
            }
            result.Paragraphs.Add(rows[i].Copy());
            offset += text.Length + 2;
            i++;
        }
        if (replacements.Count == 0) return document;
        var endOffset = MapOffset(selectionStart + selectionLength);
        selectionStart = MapOffset(selectionStart);
        selectionLength = endOffset - selectionStart;
        return result;

        int MapOffset(int original)
        {
            var mapped = original;
            foreach (var range in replacements)
            {
                if (original <= range.Start) break;
                if (original < range.Start + range.OldLength)
                    return mapped - (original - range.Start) + Math.Min(original - range.Start, range.NewLength);
                mapped += range.NewLength - range.OldLength;
            }
            return mapped;
        }
    }

    private static string Text(NoteParagraph row) => NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)));

    private static string[] SplitRow(string text)
    {
        if (text.Contains('\r') || text.Contains('\n')) return [];
        var cells = new List<string>();
        var current = new StringBuilder();
        var pipe = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '|' or '\\')
            {
                current.Append(text[i++]).Append(text[i]);
            }
            else if (text[i] == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                pipe = true;
            }
            else current.Append(text[i]);
        }
        if (!pipe) return [];
        cells.Add(current.ToString().Trim());
        if (text.TrimStart().StartsWith('|')) cells.RemoveAt(0);
        var trimmedEnd = text.TrimEnd();
        var slashes = 0;
        for (var i = trimmedEnd.Length - 2; i >= 0 && trimmedEnd[i] == '\\'; i--) slashes++;
        if (trimmedEnd.EndsWith('|') && slashes % 2 == 0) cells.RemoveAt(cells.Count - 1);
        return cells.ToArray();
    }

    private static IEnumerable<NoteParagraph> ParseCell(string text, NoteTableCell cell)
    {
        var paragraphs = new List<NoteParagraph>();
        var pieces = Regex.Split(text, "(<br\\s*/?>)", RegexOptions.IgnoreCase);
        var style = NoteTextStyle.None;
        var tags = new Regex("</?(?<style>b|i|u|s|code)>|</?(?<math>math)>|<a\\s+href=\"(?<destination>[^\"]*)\"\\s*>|</a\\s*>",
            RegexOptions.IgnoreCase);
        for (var lineIndex = 0; lineIndex < pieces.Length; lineIndex += 2)
        {
            var soft = lineIndex > 0 && pieces[lineIndex - 1].Contains('/');
            if (!soft) style = NoteTextStyle.None;
            var content = pieces[lineIndex];
            var kind = NoteBlockKind.Paragraph;
            var check = Regex.Match(content, "^- \\[(?<state>[ xX])\\] ");
            var number = Regex.Match(content, "^[0-9]+\\. ");
            if (check.Success) { kind = NoteBlockKind.Checklist; content = content[check.Length..]; }
            else if (number.Success) { kind = NoteBlockKind.Numbered; content = content[number.Length..]; }
            else if (content.StartsWith("• ", StringComparison.Ordinal) || content.StartsWith("- ", StringComparison.Ordinal) || content.StartsWith("* ", StringComparison.Ordinal))
            { kind = NoteBlockKind.Bulleted; content = content[2..]; }
            var runs = new List<NoteTextRun>();
            var offset = 0;
            var inlineKind = NoteInlineKind.Text;
            string? linkDestination = null;
            foreach (Match tag in tags.Matches(content))
            {
                var closing = tag.Value.StartsWith("</", StringComparison.Ordinal);
                if (tag.Groups["style"].Success)
                {
                    Add(content[offset..tag.Index]);
                    var flag = tag.Groups["style"].Value.ToLowerInvariant() switch
                    {
                        "b" => NoteTextStyle.Bold, "i" => NoteTextStyle.Italic, "u" => NoteTextStyle.Underline,
                        "s" => NoteTextStyle.Strikethrough, _ => NoteTextStyle.Code
                    };
                    style = closing ? style & ~flag : style | flag;
                    offset = tag.Index + tag.Length;
                }
                else if (tag.Groups["math"].Success)
                {
                    if (!closing && inlineKind == NoteInlineKind.Text &&
                        content.IndexOf("</math>", tag.Index + tag.Length, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Add(content[offset..tag.Index]);
                        inlineKind = NoteInlineKind.InlineMath;
                        offset = tag.Index + tag.Length;
                    }
                    else if (closing && inlineKind == NoteInlineKind.InlineMath)
                    {
                        Add(content[offset..tag.Index]);
                        inlineKind = NoteInlineKind.Text;
                        offset = tag.Index + tag.Length;
                    }
                }
                else if (tag.Groups["destination"].Success)
                {
                    var destination = WebUtility.HtmlDecode(tag.Groups["destination"].Value)
                        .Replace("\\|", "|", StringComparison.Ordinal);
                    if (inlineKind == NoteInlineKind.Text && NoteLinkFormatting.IsValidDestination(destination) &&
                        content.IndexOf("</a>", tag.Index + tag.Length, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Add(content[offset..tag.Index]);
                        inlineKind = NoteInlineKind.Link;
                        linkDestination = destination;
                        offset = tag.Index + tag.Length;
                    }
                }
                else if (closing && inlineKind == NoteInlineKind.Link)
                {
                    Add(content[offset..tag.Index]);
                    inlineKind = NoteInlineKind.Text;
                    linkDestination = null;
                    offset = tag.Index + tag.Length;
                }
            }
            Add(content[offset..]);
            if (soft && kind == NoteBlockKind.Paragraph && paragraphs.Count > 0)
            {
                var previous = paragraphs[^1];
                var previousRun = previous.Runs.LastOrDefault();
                var nextRun = runs.FirstOrDefault();
                var sameInline = previousRun is not null && nextRun is not null &&
                    previousRun.Kind == nextRun.Kind && previousRun.LinkDestination == nextRun.LinkDestination;
                previous.Runs.Add(new NoteTextRun("\n", sameInline ? previousRun!.Style : NoteTextStyle.None,
                    sameInline ? previousRun!.Kind : NoteInlineKind.Text,
                    sameInline ? previousRun!.LinkDestination : null));
                previous.Runs.AddRange(runs);
            }
            else paragraphs.Add(new NoteParagraph { Cell = cell, Kind = kind, IsChecked = check.Success && check.Groups["state"].Value != " ", Runs = runs });

            void Add(string value)
            {
                if (value.Length == 0) return;
                if (runs.Count > 0 && runs[^1].Style == style && runs[^1].Kind == inlineKind &&
                    runs[^1].LinkDestination == linkDestination)
                    runs[^1] = runs[^1] with { Text = runs[^1].Text + value };
                else runs.Add(new NoteTextRun(value, style, inlineKind, linkDestination));
            }
        }
        // Interpret source markers before decoding entities that represent literal characters.
        var document = new NotesDocument { Paragraphs = paragraphs };
        var start = document.GetPlainText().Length;
        var length = 0;
        var formatted = NoteCodeFormatting.Apply(document, ref start, ref length);
        formatted = NoteEmphasisFormatting.Apply(formatted, (0, formatted.GetPlainText().Length), ref start, ref length);
        formatted = NoteLinkFormatting.Apply(formatted, (0, formatted.GetPlainText().Length), ref start, ref length,
            pasted: true);
        formatted = NoteInlineMathFormatting.Apply(formatted, (0, formatted.GetPlainText().Length), ref start, ref length);
        foreach (var paragraph in formatted.Paragraphs)
            paragraph.Runs = paragraph.Runs.Select(run => run with
            {
                Text = WebUtility.HtmlDecode(Regex.Replace(run.Text, @"\\([|\\])", "$1"))
            }).ToList();
        return formatted.Paragraphs;
    }

    public static string Write(NotesDocument document, Guid tableId)
    {
        var rows = document.Paragraphs.Where(row => row.Cell?.TableId == tableId).GroupBy(row => row.Cell!.Row).ToArray();
        if (rows.Length == 0) throw new ArgumentException("The requested table does not exist.", nameof(tableId));
        var lines = new List<string>();
        foreach (var row in rows)
        {
            lines.Add("| " + string.Join(" | ", row.GroupBy(item => item.Cell!.Column).Select(cell => string.Join("<br>", cell.Select(WriteParagraph)))) + " |");
            if (row.Key == 0)
                lines.Add("| " + string.Join(" | ", row.GroupBy(item => item.Cell!.Column).Select(cell => cell.First().Cell!.Alignment switch
                { NoteCellAlignment.Center => ":---:", NoteCellAlignment.Right => "---:", _ => "---" })) + " |");
        }
        return string.Join("\r\n", lines);
    }

    private static string WriteParagraph(NoteParagraph paragraph)
    {
        var prefix = paragraph.Kind switch
        {
            NoteBlockKind.Bulleted => "• ", NoteBlockKind.Numbered => "1. ",
            NoteBlockKind.Checklist => paragraph.IsChecked ? "- [x] " : "- [ ] ", _ => ""
        };
        var result = new StringBuilder(prefix);
        foreach (var run in paragraph.Runs)
        {
            var tags = new[] { (NoteTextStyle.Bold, "b"), (NoteTextStyle.Italic, "i"), (NoteTextStyle.Underline, "u"),
                (NoteTextStyle.Strikethrough, "s"), (NoteTextStyle.Code, "code") }.Where(pair => run.Style.HasFlag(pair.Item1)).ToArray();
            var lines = NormalizeLineBreaks(run.Text).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                if (line > 0) result.Append("<br/>");
                foreach (var tag in tags) result.Append('<').Append(tag.Item2).Append('>');
                if (run.Kind == NoteInlineKind.Link && NoteLinkFormatting.IsValidDestination(run.LinkDestination))
                {
                    var destination = WebUtility.HtmlEncode(run.LinkDestination!).Replace("|", "&#124;", StringComparison.Ordinal);
                    result.Append("<a href=\"").Append(destination).Append("\">")
                        .Append(EncodeTableText(lines[line], escapeMarkdown: false)).Append("</a>");
                }
                else if (run.Kind == NoteInlineKind.InlineMath)
                {
                    result.Append("<math>").Append(EncodeTableText(lines[line], escapeMarkdown: false)).Append("</math>");
                }
                else result.Append(EncodeTableText(lines[line], escapeMarkdown: true));
                foreach (var tag in tags.Reverse()) result.Append("</").Append(tag.Item2).Append('>');
            }
        }
        // A literal list-looking paragraph must not become a list after a clipboard round trip.
        if (prefix.Length == 0 && Regex.IsMatch(result.ToString(), "^(?:[•*-] |[0-9]+\\. )"))
            return $"&#{(int)result[0]};" + result.ToString()[1..];
        return result.ToString();
    }

    private static string EncodeTableText(string text, bool escapeMarkdown)
    {
        var encoded = WebUtility.HtmlEncode(text).Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal).Replace("\t", "&#9;", StringComparison.Ordinal);
        if (escapeMarkdown)
        {
            encoded = encoded.Replace("*", "&#42;", StringComparison.Ordinal)
                .Replace("_", "&#95;", StringComparison.Ordinal).Replace("~", "&#126;", StringComparison.Ordinal)
                .Replace("$", "&#36;", StringComparison.Ordinal).Replace("[", "&#91;", StringComparison.Ordinal)
                .Replace("]", "&#93;", StringComparison.Ordinal).Replace("`", "&#96;", StringComparison.Ordinal);
            encoded = Regex.Replace(encoded, @"(?<![\p{L}\p{N}_@])https?://", match =>
                $"&#{(int)match.Value[0]};" + match.Value[1..], RegexOptions.IgnoreCase);
        }
        return Regex.Replace(encoded, "^( +)|( +)$", match => string.Concat(Enumerable.Repeat("&#32;", match.Length)));
    }
}
