using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteTableTests
{
    private const string Markdown = "| Name | Status | Action Points |\r\n| --- | :---: | ---: |\r\n| Alice | Open | • Call<br>• Email |";

    public static NotesDocument Parse(string text = Markdown)
    {
        var start = text.Length;
        var length = 0;
        return NoteTableMarkdown.Apply(NotesDocument.FromText(text), true, ref start, ref length);
    }

    [TestMethod]
    public void MarkdownCreatesAlignedCellsAndActualBulletItems()
    {
        var document = Parse();
        NotesSnapshotValidator.ValidateDocument(document);
        Assert.AreEqual(7, document.Paragraphs.Count);
        Assert.AreEqual(NoteCellAlignment.Center, document.Paragraphs[1].Cell!.Alignment);
        Assert.AreEqual(NoteCellAlignment.Right, document.Paragraphs[2].Cell!.Alignment);
        Assert.IsTrue(document.Paragraphs.TakeLast(2).All(row => row.Kind == NoteBlockKind.Bulleted));
        Assert.AreEqual("Name\r\nStatus\r\nAction Points\r\nAlice\r\nOpen\r\nCall\r\nEmail", document.GetPlainText());
    }

    [TestMethod]
    public void TypedTableWaitsForBlankLineAndPastedTableRendersWithoutOne()
    {
        var source = NotesDocument.FromText(Markdown);
        var start = Markdown.Length;
        var length = 0;
        Assert.AreSame(source, NoteTableMarkdown.Apply(source, false, ref start, ref length));
        source = NotesDocument.FromText("before\r\n" + Markdown + "\r\n\r\nafter");
        start = source.GetPlainText().Length;
        var result = NoteTableMarkdown.Apply(source, false, ref start, ref length);
        Assert.AreEqual("before", result.Paragraphs[0].Runs.Single().Text);
        Assert.AreEqual("after", result.Paragraphs[^1].Runs.Single().Text);
        Assert.AreEqual(result.GetPlainText().Length, start);
        Assert.IsNotNull(result.Paragraphs[1].Cell);
    }

    [TestMethod]
    [DataRow("| A | B |\r\n| --- | --- |\r\n| only one |")]
    [DataRow("| A | B |\r\n| -- | --- |\r\n| one | two |")]
    [DataRow("```\r\n| A | B |\r\n| --- | --- |\r\n| one | two |\r\n```")]
    public void InvalidOrCodeFencedTablesStayLiteral(string text)
    {
        var source = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;
        Assert.AreSame(source, NoteTableMarkdown.Apply(source, true, ref start, ref length));
    }

    [TestMethod]
    public void ClipboardMarkdownPreservesSpecialCharactersStylesListsAndLiteralListMarkers()
    {
        var document = Parse("| Head | Other |\r\n| --- | --- |\r\n| &lt;br&gt; \\| C:\\\\Temp | - One<br>- Two |\r\n| &#8226; literal | <b><u>bold &amp; underlined</u></b><br><code>a`b</code> |");
        var id = document.Paragraphs[0].Cell!.TableId;
        var restored = Parse(NoteTableMarkdown.Write(document, id));
        Assert.AreEqual(document.GetPlainText(), restored.GetPlainText());
        CollectionAssert.AreEqual(document.Paragraphs.Select(row => row.Kind).ToArray(), restored.Paragraphs.Select(row => row.Kind).ToArray());
        CollectionAssert.AreEqual(document.Paragraphs.SelectMany(row => row.Runs).ToArray(), restored.Paragraphs.SelectMany(row => row.Runs).ToArray());
        NotesSnapshotValidator.ValidateDocument(restored);
    }

    [TestMethod]
    public void StructuralEditsKeepRectangularTablesAndPreserveRemainingContent()
    {
        var document = Parse();
        var id = document.Paragraphs[0].Cell!.TableId;
        document = NoteTables.Change(document, id, NoteTableChange.InsertRow, 1);
        Assert.AreEqual(3, document.Paragraphs.Select(row => row.Cell!.Row).Distinct().Count());
        document = NoteTables.Change(document, id, NoteTableChange.InsertColumn, 1);
        Assert.AreEqual(4, document.Paragraphs.Select(row => row.Cell!.Column).Distinct().Count());
        document = NoteTables.Change(document, id, NoteTableChange.DeleteRow, 1);
        document = NoteTables.Change(document, id, NoteTableChange.DeleteColumn, 1);
        Assert.AreEqual(Parse().GetPlainText(), document.GetPlainText());
        document = NoteTables.Change(document, id, NoteTableChange.DeleteRow, 0);
        Assert.IsTrue(document.Paragraphs.All(row => row.Cell!.Row == 0));
        document = NoteTables.Change(document, id, NoteTableChange.DeleteRow, 0);
        Assert.AreEqual(1, document.Paragraphs.Count);
        Assert.IsNull(document.Paragraphs[0].Cell);
    }

    [TestMethod]
    public void FormattingAndSplittingCellParagraphsRetainTheirPositions()
    {
        var source = Parse();
        var (start, length) = source.GetParagraphRange(5);
        var formatted = source.SetStyle(start, length, NoteTextStyle.Bold, true).SplitParagraph(5, 2);
        NotesSnapshotValidator.ValidateDocument(formatted);
        Assert.AreEqual(formatted.Paragraphs[5].Cell, formatted.Paragraphs[6].Cell);
        Assert.AreEqual(source.Paragraphs[5].Cell, formatted.Paragraphs[5].Cell);
    }

    [TestMethod]
    public void InvalidCellCoordinatesAndNoncontiguousTablesAreRejected()
    {
        var source = Parse();
        source.Paragraphs[3].Cell = source.Paragraphs[3].Cell! with { Column = 9 };
        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.ValidateDocument(source));
        source = Parse();
        source.Paragraphs.Insert(1, new NoteParagraph());
        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.ValidateDocument(source));
    }

    [TestMethod]
    public void MarkdownRoundTripKeepsFormattedSoftBreaksAndBulletContinuations()
    {
        var source = Parse();
        source.Paragraphs[5].Runs = [new NoteTextRun("Call\nsecond line", NoteTextStyle.Bold | NoteTextStyle.Italic)];
        var restored = Parse(NoteTableMarkdown.Write(source, source.Paragraphs[0].Cell!.TableId));
        Assert.AreEqual(source.GetPlainText(), restored.GetPlainText());
        Assert.AreEqual(source.Paragraphs.Count, restored.Paragraphs.Count);
        Assert.AreEqual(source.Paragraphs[5].Kind, restored.Paragraphs[5].Kind);
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic, restored.Paragraphs[5].Runs.Last().Style);
    }

    [TestMethod]
    public void EscapedTrailingPipesAndMeaningfulWhitespaceSurviveMarkdown()
    {
        var document = Parse("A | B\r\n--- | ---\r\nleft | right\\|");
        Assert.AreEqual("right|", document.Paragraphs.Last().Runs.Single().Text);
        document.Paragraphs.Last().Runs = [new NoteTextRun("  spaced\ttext  ")];
        var copied = Parse(NoteTableMarkdown.Write(document, document.Paragraphs[0].Cell!.TableId));
        Assert.AreEqual(document.GetPlainText(), copied.GetPlainText());
    }
}
