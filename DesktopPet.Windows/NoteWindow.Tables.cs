using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;

namespace DesktopPet.Windows;

public partial class NoteWindow
{
    private bool _tablePasteRequested;

    private void CreateTableAtCaret()
    {
        try
        {
            ApplyPendingMarkupFormatting();
            var document = ReadEditorDocument();
            var (start, _) = GetSelectionOffsets(document);
            var (index, offset) = document.GetParagraphPositionAt(start);
            var source = document.Paragraphs[index];
            if (source.Cell is { } cell)
                index = document.Paragraphs.FindLastIndex(row => row.Cell?.TableId == cell.TableId) + 1;
            else if (source.Kind == NoteBlockKind.Code)
                index++;
            else
            {
                document = document.SplitParagraph(index, offset);
                if (offset > 0) index++;
                else document.Paragraphs.RemoveAt(index);
            }
            document.Paragraphs.Insert(index, new NoteParagraph { Cell = new NoteTableCell(Guid.NewGuid(), 0, 0) });
            CommitTableEdit(document, document.GetParagraphRange(index).Start);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
        }
    }

    private void InitializeTableEditing()
    {
        Editor.ContextMenu = CreateEditorMenu();
        Editor.SizeChanged += (_, _) => UpdateTableWidths();
        DataObject.AddPastingHandler(Editor, OnTablePaste);
        CommandManager.AddPreviewExecutedHandler(Editor, OnTableCopy);
    }

    private System.Windows.Controls.ContextMenu CreateEditorMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        NotesAppearance.Apply(menu, _appearanceSettings.Theme, _uiStyle);
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Cu_t", Command = ApplicationCommands.Cut, CommandTarget = Editor });
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "_Copy", Command = ApplicationCommands.Copy, CommandTarget = Editor });
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "_Paste", Command = ApplicationCommands.Paste, CommandTarget = Editor });
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateTableMenuItem());
        return menu;
    }

    private System.Windows.Controls.MenuItem CreateTableMenuItem()
    {
        var item = new System.Windows.Controls.MenuItem { Header = "Create _table" };
        item.Click += (_, _) => CreateTableAtCaret();
        return item;
    }

    private void OnTablePaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.Contains('|')) return;
        var source = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;
        if (ReferenceEquals(source, NoteTableMarkdown.Apply(source, true, ref start, ref length))) return;
        // Use the Markdown source rather than a browser's unrelated HTML clipboard representation.
        e.FormatToApply = DataFormats.UnicodeText;
        _tablePasteRequested = true;
    }

    private Table CreateEditorTable(IReadOnlyList<NoteParagraph> paragraphs)
    {
        var table = new Table { Tag = paragraphs[0].Cell!.TableId, CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        var columns = paragraphs.Max(row => row.Cell!.Column) + 1;
        for (var i = 0; i < columns; i++) table.Columns.Add(new TableColumn());
        foreach (var rows in paragraphs.GroupBy(row => row.Cell!.Row))
        {
            var row = new TableRow();
            group.Rows.Add(row);
            foreach (var contents in rows.GroupBy(item => item.Cell!.Column))
            {
                var cell = new TableCell { Padding = new Thickness(6), BorderThickness = new Thickness(.5) };
                cell.SetResourceReference(TableCell.BorderBrushProperty, "NotesBorderBrush");
                if (rows.Key == 0) cell.SetResourceReference(TextElement.BackgroundProperty, "NotesHoverBrush");
                cell.TextAlignment = contents.First().Cell!.Alignment switch
                { NoteCellAlignment.Center => TextAlignment.Center, NoteCellAlignment.Right => TextAlignment.Right, _ => TextAlignment.Left };
                row.Cells.Add(cell);
                AddEditorBlocks(cell.Blocks, contents.ToArray());
                cell.ContextMenu = CreateTableMenu(cell);
            }
        }
        return table;
    }

    private void UpdateTableWidths()
    {
        var previous = _updatingEditor;
        _updatingEditor = true;
        try
        {
            var available = Math.Max(1, (Editor.ActualWidth > 0 ? Editor.ActualWidth : Width - 24) - Editor.Padding.Left - Editor.Padding.Right - 2);
            foreach (var table in Editor.Document.Blocks.OfType<Table>())
                foreach (var column in table.Columns) column.Width = new GridLength(available / table.Columns.Count);
        }
        finally { _updatingEditor = previous; }
    }

    private System.Windows.Controls.ContextMenu CreateTableMenu(TableCell cell)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        NotesAppearance.Apply(menu, _appearanceSettings.Theme, _uiStyle);
        Add("Insert row above", NoteTableChange.InsertRow, after: false);
        Add("Insert row below", NoteTableChange.InsertRow, after: true);
        Add("Insert column left", NoteTableChange.InsertColumn, after: false);
        Add("Insert column right", NoteTableChange.InsertColumn, after: true);
        menu.Items.Add(new Separator());
        Add("Delete row", NoteTableChange.DeleteRow, after: false);
        Add("Delete column", NoteTableChange.DeleteColumn, after: false);
        menu.Items.Add(new Separator());
        var copy = new System.Windows.Controls.MenuItem { Header = "Copy table as Markdown" };
        copy.Click += (_, _) => CopyTable((Guid)CellTable(cell).Tag);
        menu.Items.Add(copy);
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateTableMenuItem());
        return menu;

        void Add(string label, NoteTableChange change, bool after)
        {
            var item = new System.Windows.Controls.MenuItem { Header = label };
            item.Click += (_, _) =>
            {
                var row = (TableRow)cell.Parent;
                var group = (TableRowGroup)row.Parent;
                var index = change is NoteTableChange.InsertRow or NoteTableChange.DeleteRow
                    ? group.Rows.IndexOf(row) : row.Cells.IndexOf(cell);
                ChangeTable((Guid)CellTable(cell).Tag, change, index + (after ? 1 : 0));
            };
            menu.Items.Add(item);
        }
    }

    private static Table CellTable(TableCell cell) => (Table)((TableRowGroup)((TableRow)cell.Parent).Parent).Parent;

    private void ChangeTable(Guid id, NoteTableChange change, int index)
    {
        try
        {
            var document = ReadEditorDocument();
            var updated = NoteTables.Change(document, id, change, index);
            var target = updated.Paragraphs.FindIndex(row => row.Cell?.TableId == id &&
                (change is NoteTableChange.InsertRow or NoteTableChange.DeleteRow ? row.Cell.Row == index : row.Cell.Column == index));
            if (target < 0) target = updated.Paragraphs.FindIndex(row => row.Cell?.TableId == id);
            if (target < 0) target = Math.Min(document.Paragraphs.FindIndex(row => row.Cell?.TableId == id), updated.Paragraphs.Count - 1);
            CommitTableEdit(updated, updated.GetParagraphRange(Math.Max(0, target)).Start);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
        }
    }

    private void CommitTableEdit(NotesDocument document, int caret)
    {
        NotesSnapshotValidator.ValidateDocument(document);
        SetEditorDocument(document, caret, 0);
        SetSaveStatus(_updateDocument(_noteId, document));
        _lastAcceptedDocument = document.Copy();
        Editor.Focus();
    }

    private bool HandleTableKey(Key key, bool control, bool shift)
    {
        if (key is not (Key.Tab or Key.Enter)) return false;
        try
        {
            var document = ReadEditorDocument();
            var (start, length) = GetSelectionOffsets(document);
            var paragraphIndex = document.GetParagraphIndexAt(start);
            var cell = document.Paragraphs[paragraphIndex].Cell;
            if (cell is null) return false;
            if (key == Key.Tab && !control)
            {
                var cells = document.Paragraphs.Where(row => row.Cell?.TableId == cell.TableId).Select(row => row.Cell!).Distinct().ToList();
                var next = cells.IndexOf(cell) + (shift ? -1 : 1);
                if (next == cells.Count)
                {
                    document = NoteTables.Change(document, cell.TableId, NoteTableChange.InsertRow, cell.Row + 1);
                    var target = document.Paragraphs.FindIndex(row => row.Cell?.TableId == cell.TableId && row.Cell.Row == cell.Row + 1);
                    CommitTableEdit(document, document.GetParagraphRange(target).Start);
                }
                else if (next >= 0)
                {
                    var target = document.Paragraphs.FindIndex(row => row.Cell == cells[next]);
                    Editor.CaretPosition = FindTextPosition(Editor.Document, document, document.GetParagraphRange(target).Start);
                }
                else if (paragraphIndex > 0)
                    Editor.CaretPosition = FindTextPosition(Editor.Document, document, document.GetParagraphRange(paragraphIndex - 1).Start);
                return true;
            }
            if (key != Key.Enter || shift || length != 0) return false;
            if (control)
            {
                var last = document.Paragraphs.FindLastIndex(row => row.Cell?.TableId == cell.TableId);
                if (last + 1 == document.Paragraphs.Count || document.Paragraphs[last + 1].Cell is not null ||
                    document.Paragraphs[last + 1].Kind != NoteBlockKind.Paragraph)
                    document.Paragraphs.Insert(last + 1, new NoteParagraph());
                CommitTableEdit(document, document.GetParagraphRange(last + 1).Start);
            }
            else
            {
                var (paragraphStart, paragraphLength) = document.GetParagraphRange(paragraphIndex);
                var source = document.Paragraphs[paragraphIndex];
                var emptyList = paragraphLength == 0 && source.Kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist;
                var updated = emptyList ? document.EndEmptyListItem(paragraphIndex) : document.SplitParagraph(paragraphIndex, start - paragraphStart);
                CommitTableEdit(updated, emptyList ? start : start + 2);
            }
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
            return true; // Consume rejected edits so WPF does not execute its default Enter or Tab action.
        }
    }

    private void OnTableCopy(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Copy || Editor.Selection.IsEmpty) return;
        if (GetSelectedTable() is { } id)
        {
            CopyTable(id);
            e.Handled = true;
        }
    }

    private Guid? GetSelectedTable()
    {
        var document = ReadEditorDocument();
        var (start, length) = GetSelectionOffsets(document);
        foreach (var id in document.Paragraphs.Where(row => row.Cell is not null).Select(row => row.Cell!.TableId).Distinct())
        {
            var first = document.Paragraphs.FindIndex(row => row.Cell?.TableId == id);
            var last = document.Paragraphs.FindLastIndex(row => row.Cell?.TableId == id);
            var tableStart = document.GetParagraphRange(first).Start;
            var lastRange = document.GetParagraphRange(last);
            if (start > tableStart || start + length < lastRange.Start + lastRange.Length) continue;
            var includesOtherContent = document.Paragraphs.Select((row, index) => (row, range: document.GetParagraphRange(index)))
                .Any(item => item.row.Cell?.TableId != id && item.range.Start < start + length &&
                    (item.range.Start + item.range.Length > start || (item.row.Cell is not null && item.range.Start >= start)));
            if (!includesOtherContent) return id;
        }
        return null;
    }

    private void RefreshTableMenus()
    {
        if (Editor.ContextMenu is { } editorMenu)
            NotesAppearance.Apply(editorMenu, _appearanceSettings.Theme, _uiStyle);
        foreach (var cell in Editor.Document.Blocks.OfType<Table>().SelectMany(table => table.RowGroups)
                     .SelectMany(group => group.Rows).SelectMany(row => row.Cells))
            if (cell.ContextMenu is { } menu)
                NotesAppearance.Apply(menu, _appearanceSettings.Theme, _uiStyle);
    }

    private void CopyTable(Guid id)
    {
        try { System.Windows.Clipboard.SetText(NoteTableMarkdown.Write(ReadEditorDocument(), id)); }
        catch (ExternalException)
        {
            System.Windows.MessageBox.Show(this, "The clipboard is busy. Try copying the table again.", "Copy table", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
