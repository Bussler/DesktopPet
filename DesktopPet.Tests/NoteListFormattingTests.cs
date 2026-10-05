using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteListFormattingTests
{
    [TestMethod]
    [DataRow("- ", NoteBlockKind.Bulleted)]
    [DataRow("1) ", NoteBlockKind.Numbered)]
    [DataRow("- [ ] ", NoteBlockKind.Checklist)]
    public void CompletedMarkerPreservesContentStylesAndMapsSelection(string marker, NoteBlockKind kind)
    {
        var document = new NotesDocument { Paragraphs = [new NoteParagraph
        {
            Runs = [new NoteTextRun(marker[..1]), new NoteTextRun(marker[1..] + "Task", NoteTextStyle.Bold)]
        }] };
        var start = marker.Length;
        var length = 4;
        var formatted = NoteListFormatting.Apply(document, (0, marker.Length), ref start, ref length);

        Assert.AreEqual("Task", formatted.GetPlainText());
        Assert.AreEqual(kind, formatted.Paragraphs.Single().Kind);
        Assert.AreEqual(NoteTextStyle.Bold, formatted.Paragraphs.Single().Runs.Single().Style);
        Assert.IsFalse(formatted.Paragraphs.Single().IsChecked);
        Assert.AreEqual(0, start);
        Assert.AreEqual(4, length);
        Assert.AreEqual(marker + "Task", document.GetPlainText(), "Formatting must not mutate its source.");
    }

    [TestMethod]
    public void PastedListsMapCaretAcrossMultipleParagraphsAndKeepNeighbors()
    {
        var document = NotesDocument.FromText("before\r\n- One\r\n1) Two\r\n- [ ] Three\r\nafter");
        var start = document.GetParagraphRange(3).Start + 11;
        var length = 0;
        var formatted = NoteListFormatting.Apply(document, (8, start - 8), ref start, ref length);
        Assert.AreEqual("before\r\nOne\r\nTwo\r\nThree\r\nafter", formatted.GetPlainText());
        CollectionAssert.AreEqual(new[] { NoteBlockKind.Paragraph, NoteBlockKind.Bulleted, NoteBlockKind.Numbered,
            NoteBlockKind.Checklist, NoteBlockKind.Paragraph }, formatted.Paragraphs.Select(row => row.Kind).ToArray());
        Assert.AreEqual(formatted.GetParagraphRange(3).Start + 5, start);
    }

    [TestMethod]
    public void ChecklistCompletionConvertsAnExistingBulletAndPreservesItsCell()
    {
        var cell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        var document = new NotesDocument { Paragraphs = [new NoteParagraph
            { Cell = cell, Kind = NoteBlockKind.Bulleted, Runs = [new NoteTextRun("[ ] Task")] }] };
        var start = 4;
        var length = 0;
        var formatted = NoteListFormatting.Apply(document, (3, 1), ref start, ref length);
        Assert.AreEqual(NoteBlockKind.Checklist, formatted.Paragraphs.Single().Kind);
        Assert.AreEqual("Task", formatted.GetPlainText());
        Assert.AreEqual(cell, formatted.Paragraphs.Single().Cell);
        Assert.AreEqual(0, start);
    }

    [TestMethod]
    [DataRow("-", 0, 1)]
    [DataRow("1)", 0, 2)]
    [DataRow(" - ", 0, 3)]
    [DataRow("Text - ", 0, 7)]
    [DataRow("- literal", 2, 7)]
    [DataRow("1) literal", 3, 7)]
    [DataRow("- ", 1, 0)]
    [DataRow("```cs\r\n- ", 9, 1)]
    [DataRow("```\r\n1) ", 7, 1)]
    [DataRow("```\r\n- [ ] ", 10, 1)]
    public void IncompleteEmbeddedExistingDeletedAndFencedMarkersStayLiteral(string text, int inputStart, int inputLength)
    {
        var document = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;
        Assert.AreSame(document, NoteListFormatting.Apply(document, (inputStart, inputLength), ref start, ref length));
        Assert.AreEqual(text.Length, start);
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Code, NoteTextStyle.None)]
    [DataRow(NoteBlockKind.Paragraph, NoteTextStyle.Code)]
    [DataRow(NoteBlockKind.Numbered, NoteTextStyle.None)]
    [DataRow(NoteBlockKind.Checklist, NoteTextStyle.None)]
    public void CodeAndExistingListItemsStayLiteral(NoteBlockKind kind, NoteTextStyle style)
    {
        var document = new NotesDocument { Paragraphs = [new NoteParagraph
            { Kind = kind, Runs = [new NoteTextRun("- ", style)] }] };
        var start = 2;
        var length = 0;
        Assert.AreSame(document, NoteListFormatting.Apply(document, (1, 1), ref start, ref length));
    }
}
