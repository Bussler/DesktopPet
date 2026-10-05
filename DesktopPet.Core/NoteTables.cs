namespace DesktopPet.Core;

public enum NoteCellAlignment { Left, Center, Right }
public sealed record NoteTableCell(Guid TableId, int Row, int Column, NoteCellAlignment Alignment = NoteCellAlignment.Left);
public enum NoteTableChange { InsertRow, DeleteRow, InsertColumn, DeleteColumn }

public static class NoteTables
{
    public static void Validate(NotesDocument document)
    {
        var closed = new HashSet<Guid>();
        Guid? current = null;
        var positions = new List<NoteTableCell>();
        foreach (var paragraph in document.Paragraphs)
        {
            var cell = paragraph.Cell;
            if (cell?.TableId != current)
            {
                ValidatePositions(positions);
                positions.Clear();
                if (current.HasValue) closed.Add(current.Value);
                current = cell?.TableId;
                if (current.HasValue && closed.Contains(current.Value))
                    throw new InvalidDataException("Table paragraphs must stay together.");
            }
            if (cell is null) continue;
            if (cell.TableId == Guid.Empty || cell.Row < 0 || cell.Column < 0 || !Enum.IsDefined(cell.Alignment) || paragraph.Kind == NoteBlockKind.Code)
                throw new InvalidDataException("Table cells must use valid positions, alignment, and paragraph content.");
            if (positions.Count == 0 || positions[^1] != cell) positions.Add(cell);
        }
        ValidatePositions(positions);
    }

    private static void ValidatePositions(List<NoteTableCell> cells)
    {
        if (cells.Count == 0) return;
        var columns = cells.TakeWhile(cell => cell.Row == 0).Count();
        if (columns == 0 || cells.Count % columns != 0)
            throw new InvalidDataException("Every table row must have the same number of cells.");
        for (var i = 0; i < cells.Count; i++)
            if (cells[i].Row != i / columns || cells[i].Column != i % columns || cells[i].Alignment != cells[i % columns].Alignment)
                throw new InvalidDataException("Table cells must be rectangular, ordered, and use consistent column alignment.");
    }

    public static NotesDocument Change(NotesDocument document, Guid tableId, NoteTableChange change, int index)
    {
        var result = document.Copy();
        var table = result.Paragraphs.Where(paragraph => paragraph.Cell?.TableId == tableId).ToList();
        if (table.Count == 0) throw new ArgumentException("The requested table does not exist.", nameof(tableId));
        var first = result.Paragraphs.IndexOf(table[0]);
        var rows = table[^1].Cell!.Row + 1;
        var columns = table.Max(paragraph => paragraph.Cell!.Column) + 1;
        var rowChange = change is NoteTableChange.InsertRow or NoteTableChange.DeleteRow;
        var insert = change is NoteTableChange.InsertRow or NoteTableChange.InsertColumn;
        if (!Enum.IsDefined(change) || index < 0 || index >= (rowChange ? rows : columns) + (insert ? 1 : 0))
            throw new ArgumentOutOfRangeException(nameof(index));
        result.Paragraphs.RemoveRange(first, table.Count);
        var updated = new List<NoteParagraph>();
        foreach (var paragraph in table)
        {
            var cell = paragraph.Cell!;
            var position = rowChange ? cell.Row : cell.Column;
            if (!insert && position == index) continue;
            var shifted = position >= index ? position + (insert ? 1 : -1) : position;
            paragraph.Cell = rowChange ? cell with { Row = shifted } : cell with { Column = shifted };
            updated.Add(paragraph);
        }
        if (insert)
            for (var other = 0; other < (rowChange ? columns : rows); other++)
            {
                var alignment = rowChange ? table.First(paragraph => paragraph.Cell!.Column == other).Cell!.Alignment : NoteCellAlignment.Left;
                updated.Add(new NoteParagraph { Cell = new(tableId, rowChange ? index : other, rowChange ? other : index, alignment) });
            }
        result.Paragraphs.InsertRange(first, updated.OrderBy(paragraph => paragraph.Cell!.Row).ThenBy(paragraph => paragraph.Cell!.Column));
        if (result.Paragraphs.Count == 0) result.Paragraphs.Add(new NoteParagraph());
        NotesSnapshotValidator.ValidateDocument(result);
        return result;
    }
}
