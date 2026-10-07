using System.IO;
using System.Text.Json;
using System.Windows;
using DesktopPet.Core;
using static DesktopPet.Core.NoteTextOffsets;
using DataObject = System.Windows.DataObject;

namespace DesktopPet.Windows;

public partial class NoteWindow
{
    private const string ListClipboardFormat = "Hood.Notes.List.v1";
    private const string SemanticClipboardFormat = "Hood.Notes.Semantic.v1";

    private void InitializeListClipboard()
    {
        DataObject.AddCopyingHandler(Editor, OnListCopy);
        DataObject.AddPastingHandler(Editor, OnListPaste);
    }

    private void OnListCopy(object sender, DataObjectCopyingEventArgs e)
    {
        if (e.IsDragDrop || Editor.Selection.IsEmpty) return;
        var document = ReadEditorDocument();
        var (start, length) = GetSelectionOffsets(document);
        var (first, _) = document.GetParagraphPositionAt(start);
        var (last, _) = document.GetParagraphPositionAt(start + length);
        var rows = document.Paragraphs.Skip(first).Take(last - first + 1).ToArray();
        var containsList = rows.Any(row =>
            row.Kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist);
        var touchesMath = SelectionTouchesMath(document, start, length);
        var touchesLink = SelectionTouchesLink(document, start, length);
        if (!containsList && !touchesMath && !touchesLink) return;

        var firstRange = document.GetParagraphRange(first);
        // Native inline paste preserves the destination's paragraph kind and checked state.
        var fullListItem = first != last || start == firstRange.Start && length >= firstRange.Length;
        var selected = SelectParagraphs(document, start, length, first, last);
        if (touchesMath || touchesLink)
        {
            var plainText = ProjectMathClipboardText(document, start, length);
            e.DataObject.SetData(System.Windows.DataFormats.UnicodeText, plainText);
            e.DataObject.SetData(System.Windows.DataFormats.Text, plainText);
            if (selected.Paragraphs.SelectMany(row => row.Runs).Any(run => run.Kind == NoteInlineKind.InlineMath) ||
                selected.Paragraphs.SelectMany(row => row.Runs).Any(run => run.Kind == NoteInlineKind.Link) ||
                SelectionContainsWholeDisplayMath(document, start, length))
                e.DataObject.SetData(SemanticClipboardFormat, JsonSerializer.Serialize(selected));
        }
        if (containsList && fullListItem)
        {
            // WPF omits single-item list structure and replaces inline controls with spaces.
            // Keep its standard clipboard formats for other applications, plus our note data.
            e.DataObject.SetData(ListClipboardFormat, JsonSerializer.Serialize(selected));
        }
    }

    private void OnListPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.IsDragDrop) return;
        var listPayload = e.DataObject.GetData(ListClipboardFormat) as string;
        var semanticPayload = e.DataObject.GetData(SemanticClipboardFormat) as string;
        var payload = listPayload ?? semanticPayload;
        if (payload is null) return;
        try
        {
            var selected = JsonSerializer.Deserialize<NotesDocument>(payload);
            if (selected is null) return;
            NotesSnapshotValidator.ValidateDocument(selected);
            if (selected.Paragraphs.Any(row => row.Cell is not null)) return;
            var pastedLength = selected.GetPlainText().Length;
            var document = ReadEditorDocument();
            var (start, length) = GetSelectionOffsets(document);
            if (listPayload is null && !CanPasteSemanticContent(document, start, length, selected))
            {
                e.FormatToApply = System.Windows.DataFormats.UnicodeText;
                return;
            }
            var (first, firstOffset) = document.GetParagraphPositionAt(start);
            var (last, lastOffset) = document.GetParagraphPositionAt(start + length);
            var cell = document.Paragraphs[first].Cell;
            if (document.Paragraphs.Skip(first).Take(last - first + 1).Any(row => row.Cell != cell)) return;
            foreach (var row in selected.Paragraphs) row.Cell = cell;
            var prefix = SliceRuns(document.Paragraphs[first], 0, firstOffset);
            var suffix = SliceRuns(document.Paragraphs[last], lastOffset, document.GetParagraphRange(last).Length);
            selected.Paragraphs[0].Runs.InsertRange(0, prefix);
            selected.Paragraphs[^1].Runs.AddRange(suffix);
            document.Paragraphs.RemoveRange(first, last - first + 1);
            document.Paragraphs.InsertRange(first, selected.Paragraphs);
            NotesSnapshotValidator.ValidateDocument(document);
            e.CancelCommand();
            // The paste event runs inside WPF's edit transaction; rebuild after it ends.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
            {
                SetEditorDocument(document, start + pastedLength, 0);
                SetSaveStatus(_updateDocument(_noteId, document));
                _lastAcceptedDocument = document.Copy();
                UpdateToolbar();
            }));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
        {
            e.CancelCommand();
            SetSaveStatus(exception.Message);
        }
    }

    private static List<NoteTextRun> SliceRuns(NoteParagraph paragraph, int start, int end)
    {
        var runs = new List<NoteTextRun>();
        var offset = 0;
        foreach (var run in paragraph.Runs)
        {
            var text = NormalizeLineBreaks(run.Text);
            var from = Math.Clamp(start - offset, 0, text.Length);
            var to = Math.Clamp(end - offset, from, text.Length);
            if (to > from)
            {
                var kind = run.Kind == NoteInlineKind.InlineMath && (from != 0 || to != text.Length)
                    ? NoteInlineKind.Text
                    : run.Kind;
                runs.Add(run with
                {
                    Text = text[from..to],
                    Kind = kind,
                    LinkDestination = kind == NoteInlineKind.Link ? run.LinkDestination : null
                });
            }
            offset += text.Length;
        }
        return runs;
    }

    private static NotesDocument SelectParagraphs(NotesDocument document, int start, int length, int first, int last)
    {
        var selected = new NotesDocument { Paragraphs = [] };
        for (var index = first; index <= last; index++)
        {
            var range = document.GetParagraphRange(index);
            var paragraph = document.Paragraphs[index];
            selected.Paragraphs.Add(new NoteParagraph
            {
                Kind = paragraph.Kind,
                IsChecked = paragraph.IsChecked,
                Runs = SliceRuns(paragraph, Math.Max(0, start - range.Start),
                    Math.Min(range.Length, start + length - range.Start))
            });
        }
        return selected;
    }

    private static bool SelectionTouchesMath(NotesDocument document, int start, int length)
    {
        var end = start + length;
        for (var index = 0; index < document.Paragraphs.Count; index++)
        {
            var paragraph = document.Paragraphs[index];
            var (paragraphStart, paragraphLength) = document.GetParagraphRange(index);
            if (paragraph.Kind == NoteBlockKind.DisplayMath && start < paragraphStart + paragraphLength && end > paragraphStart)
                return true;
            var offset = paragraphStart;
            foreach (var run in paragraph.Runs)
            {
                var runLength = NormalizeLineBreaks(run.Text).Length;
                if (run.Kind == NoteInlineKind.InlineMath && start < offset + runLength && end > offset)
                    return true;
                offset += runLength;
            }
        }
        return false;
    }

    private static bool SelectionTouchesLink(NotesDocument document, int start, int length)
    {
        var end = start + length;
        var offset = 0;
        foreach (var paragraph in document.Paragraphs)
        {
            foreach (var run in paragraph.Runs)
            {
                var runLength = NormalizeLineBreaks(run.Text).Length;
                if (run.Kind == NoteInlineKind.Link && start < offset + runLength && end > offset) return true;
                offset += runLength;
            }
            offset += 2;
        }
        return false;
    }

    private static bool CanPasteSemanticContent(NotesDocument document, int start, int length, NotesDocument pasted)
    {
        var includesDisplayMath = pasted.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath);
        var end = start + length;
        var (first, _) = document.GetParagraphPositionAt(start);
        var (last, _) = document.GetParagraphPositionAt(end);
        for (var index = first; index <= last; index++)
        {
            var paragraph = document.Paragraphs[index];
            if (paragraph.Kind is NoteBlockKind.Code or NoteBlockKind.Divider or NoteBlockKind.DisplayMath)
                return false;
            if (includesDisplayMath && paragraph.Kind is not NoteBlockKind.Paragraph)
                return false;
            var offset = document.GetParagraphRange(index).Start;
            foreach (var run in paragraph.Runs)
            {
                var runLength = NormalizeLineBreaks(run.Text).Length;
                var overlaps = length == 0
                    ? start >= offset && start < offset + runLength
                    : start < offset + runLength && end > offset;
                if (overlaps && run.Style.HasFlag(NoteTextStyle.Code)) return false;
                offset += runLength;
            }
        }

        if (includesDisplayMath)
        {
            if (length == 0)
                return document.Paragraphs[first].Kind == NoteBlockKind.Paragraph &&
                       document.GetParagraphRange(first).Length == 0;
            var firstRange = document.GetParagraphRange(first);
            var lastRange = document.GetParagraphRange(last);
            return start == firstRange.Start && end >= lastRange.Start + lastRange.Length;
        }

        return true;
    }

    private static string ProjectMathClipboardText(NotesDocument document, int start, int length)
    {
        var result = document.GetPlainText().Substring(start, length);
        var end = start + length;
        for (var index = document.Paragraphs.Count - 1; index >= 0; index--)
        {
            var paragraph = document.Paragraphs[index];
            if (paragraph.Kind != NoteBlockKind.DisplayMath) continue;
            var (paragraphStart, paragraphLength) = document.GetParagraphRange(index);
            if (paragraphLength == 0 || start > paragraphStart || end < paragraphStart + paragraphLength) continue;
            var source = string.Concat(paragraph.Runs.Select(run => run.Text));
            var trimmed = source.Trim();
            if (trimmed.StartsWith("$$", StringComparison.Ordinal) && trimmed.EndsWith("$$", StringComparison.Ordinal)) continue;
            var relativeStart = paragraphStart - start;
            result = result[..relativeStart] + "$$" + source + "$$" + result[(relativeStart + paragraphLength)..];
        }
        return result;
    }

    private static bool SelectionContainsWholeDisplayMath(NotesDocument document, int start, int length)
    {
        var end = start + length;
        return length > 0 && document.Paragraphs.Select((paragraph, index) => (paragraph, index))
            .Any(item => item.paragraph.Kind == NoteBlockKind.DisplayMath &&
                         start <= document.GetParagraphRange(item.index).Start &&
                         end >= document.GetParagraphRange(item.index).Start +
                                document.GetParagraphRange(item.index).Length);
    }
}
