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
        if (!rows.Any(row =>
                row.Kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist)) return;

        var firstRange = document.GetParagraphRange(first);
        // Native inline paste preserves the destination's paragraph kind and checked state.
        if (first == last && (start > firstRange.Start || length < firstRange.Length)) return;
        var selected = new NotesDocument { Paragraphs = [] };
        var paragraphStart = firstRange.Start;
        for (var index = first; index <= last; index++)
        {
            var row = document.Paragraphs[index];
            var paragraphLength = NormalizeLineBreaks(string.Concat(row.Runs.Select(run => run.Text))).Length;
            selected.Paragraphs.Add(new NoteParagraph
            {
                Kind = row.Kind,
                IsChecked = row.IsChecked,
                Runs = SliceRuns(row, Math.Max(0, start - paragraphStart),
                    Math.Min(paragraphLength, start + length - paragraphStart))
            });
            paragraphStart += paragraphLength + 2;
        }
        // WPF omits single-item list structure and replaces inline controls with spaces.
        // Keep its standard clipboard formats for other applications, plus our note data.
        e.DataObject.SetData(ListClipboardFormat, JsonSerializer.Serialize(selected));
    }

    private void OnListPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.IsDragDrop || e.DataObject.GetData(ListClipboardFormat) is not string payload) return;
        try
        {
            var selected = JsonSerializer.Deserialize<NotesDocument>(payload);
            if (selected is null) return;
            NotesSnapshotValidator.ValidateDocument(selected);
            if (selected.Paragraphs.Any(row => row.Cell is not null)) return;
            var pastedLength = selected.GetPlainText().Length;
            var document = ReadEditorDocument();
            var (start, length) = GetSelectionOffsets(document);
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
            if (to > from) runs.Add(run with { Text = text[from..to] });
            offset += text.Length;
        }
        return runs;
    }
}
