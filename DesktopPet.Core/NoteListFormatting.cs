using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Core;

public static class NoteListFormatting
{
    public static NotesDocument Apply(NotesDocument document, (int Start, int Length) input,
        ref int selectionStart, ref int selectionLength)
    {
        var result = document.Copy();
        var removed = new List<(int Start, int Length)>();
        var offset = 0;
        var insideFence = false;
        foreach (var row in result.Paragraphs)
        {
            var text = NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text)));
            if (row.Cell is null && row.Kind == NoteBlockKind.Paragraph)
            {
                if (insideFence)
                {
                    if (text.Trim() == "```") insideFence = false;
                    offset += text.Length + 2;
                    continue;
                }
                if (NoteCodeFormatting.IsOpeningFence(text)) insideFence = true;
            }

            var (marker, kind) = row.Kind switch
            {
                NoteBlockKind.Paragraph when text.StartsWith("- [ ] ", StringComparison.Ordinal) => ("- [ ] ", NoteBlockKind.Checklist),
                NoteBlockKind.Paragraph when text.StartsWith("- ", StringComparison.Ordinal) => ("- ", NoteBlockKind.Bulleted),
                NoteBlockKind.Paragraph when text.StartsWith("1) ", StringComparison.Ordinal) => ("1) ", NoteBlockKind.Numbered),
                NoteBlockKind.Bulleted when text.StartsWith("[ ] ", StringComparison.Ordinal) => ("[ ] ", NoteBlockKind.Checklist),
                _ => (string.Empty, row.Kind)
            };
            // Only newly completed markers convert; existing literal text survives later edits and Undo.
            var markerEnd = offset + marker.Length;
            var runOffset = 0;
            var codeMarker = row.Runs.Any(run =>
            {
                var inMarker = runOffset < marker.Length;
                runOffset += NormalizeLineBreaks(run.Text).Length;
                return inMarker && run.Style.HasFlag(NoteTextStyle.Code);
            });
            if (marker.Length > 0 && input.Start < markerEnd && input.Start + input.Length >= markerEnd &&
                !codeMarker)
            {
                var remaining = marker.Length;
                row.Runs = row.Runs.Select(run =>
                {
                    var count = Math.Min(remaining, NormalizeLineBreaks(run.Text).Length);
                    remaining -= count;
                    return run with { Text = run.Text[RawOffset(run.Text, count)..] };
                }).Where(run => run.Text.Length > 0).ToList();
                row.Kind = kind;
                row.IsChecked = false;
                removed.Add((offset, marker.Length));
            }
            offset += text.Length + 2;
        }

        if (removed.Count == 0) return document;
        var end = MapOffset(selectionStart + selectionLength);
        selectionStart = MapOffset(selectionStart);
        selectionLength = end - selectionStart;
        return result;

        int MapOffset(int position) => position - removed.Sum(range => Math.Clamp(position - range.Start, 0, range.Length));
    }
}
