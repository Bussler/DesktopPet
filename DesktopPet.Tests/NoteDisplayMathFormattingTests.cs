using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteDisplayMathFormattingTests
{
    [TestMethod]
    public void SameLineDisplayMathBecomesASeparateBlockAndKeepsNeighborText()
    {
        const string text = "before $$x_1^2$$ after";
        var document = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;

        var formatted = NoteDisplayMathFormatting.Apply(document, (0, text.Length), ref start, ref length);

        CollectionAssert.AreEqual(new[] { "before ", "$$x_1^2$$", " after" },
            formatted.Paragraphs.Select(Text).ToArray());
        CollectionAssert.AreEqual(new[] { NoteBlockKind.Paragraph, NoteBlockKind.DisplayMath, NoteBlockKind.Paragraph },
            formatted.Paragraphs.Select(row => row.Kind).ToArray());
        Assert.AreEqual("before \r\n$$x_1^2$$\r\n after", formatted.GetPlainText());
        Assert.AreEqual(formatted.GetPlainText().Length, start);
        NotesSnapshotValidator.ValidateDocument(formatted);
    }

    [TestMethod]
    public void TerminalDisplayBlockLeavesAnEditableParagraphAfterTheFormula()
    {
        const string text = "$$\\unknowncommand$$";
        var document = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;

        var formatted = NoteDisplayMathFormatting.Apply(document, (0, text.Length), ref start, ref length);

        Assert.AreEqual(NoteBlockKind.DisplayMath, formatted.Paragraphs[0].Kind);
        Assert.AreEqual(text, Text(formatted.Paragraphs[0]));
        Assert.AreEqual(NoteBlockKind.Paragraph, formatted.Paragraphs[1].Kind);
        Assert.AreEqual(formatted.GetPlainText().Length, start);
    }

    [TestMethod]
    public void OnlyNewlyCompletedDelimitersConvertAndMultipleBlocksKeepTheirOrder()
    {
        const string text = "old $$x$$ then $$y$$ end";
        var document = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;
        var secondClosing = text.LastIndexOf("$$", StringComparison.Ordinal);

        var formatted = NoteDisplayMathFormatting.Apply(document, (secondClosing, 2), ref start, ref length);

        Assert.AreEqual(NoteBlockKind.Paragraph, formatted.Paragraphs[0].Kind);
        CollectionAssert.AreEqual(new[] { "$$y$$" }, formatted.Paragraphs
            .Where(row => row.Kind == NoteBlockKind.DisplayMath).Select(Text).ToArray());
        CollectionAssert.AreEqual(new[] { "old $$x$$ then ", "$$y$$", " end" },
            formatted.Paragraphs.Select(Text).ToArray());
        Assert.AreEqual(formatted.GetPlainText().Length, start);
    }

    [TestMethod]
    public void MultilineDisplaySourceKeepsDelimiterLinesAndInteriorLineBreaksExactly()
    {
        const string source = "$$\r\n\\begin{aligned}a&=b\\\\\r\nc&=d\\end{aligned}\r\n$$";
        const string text = "before\r\n" + source + "\r\nafter";
        var document = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;
        var closing = text.LastIndexOf("$$", StringComparison.Ordinal);

        var formatted = NoteDisplayMathFormatting.Apply(document, (closing, 2), ref start, ref length);

        Assert.AreEqual("before", Text(formatted.Paragraphs[0]));
        Assert.AreEqual(NoteBlockKind.DisplayMath, formatted.Paragraphs[1].Kind);
        Assert.AreEqual(source, Text(formatted.Paragraphs[1]));
        Assert.AreEqual("after", Text(formatted.Paragraphs[2]));
        Assert.AreEqual(text, formatted.GetPlainText());
        Assert.AreEqual(text.Length, start);
    }

    [TestMethod]
    public void UnfinishedEscapedCodeAndTableMathRemainLiteral()
    {
        const string incomplete = "$$\r\n# not a heading\r\n*not emphasis*";
        var document = NotesDocument.FromText(incomplete);
        var start = incomplete.Length;
        var length = 0;
        var unchanged = NoteDisplayMathFormatting.Apply(document, (0, incomplete.Length), ref start, ref length);
        Assert.AreSame(document, unchanged);
        var notMarkdown = NoteBlockMarkdownFormatting.Apply(unchanged, (0, incomplete.Length), ref start, ref length);
        Assert.AreSame(document, notMarkdown);
        var emphasis = NoteEmphasisFormatting.Apply(document, (0, incomplete.Length), ref start, ref length);
        Assert.AreSame(document, emphasis);

        foreach (var text in new[] { "\\$$literal$$", "$\\$ not display" })
        {
            var literal = NotesDocument.FromText(text);
            start = text.Length;
            length = 0;
            Assert.AreSame(literal,
                NoteDisplayMathFormatting.Apply(literal, (0, text.Length), ref start, ref length), text);
        }

        var tableCell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        var table = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Cell = tableCell, Runs = [new NoteTextRun("$$x$$")] }]
        };
        start = 5;
        Assert.AreSame(table, NoteDisplayMathFormatting.Apply(table, (0, 5), ref start, ref length));
    }

    [TestMethod]
    public void CreateBlockReplacesExactSelectionOrSplitsAtCaret()
    {
        const string selected = "left $$x$\r\n  y$$ right";
        var document = NotesDocument.FromText(selected);
        var start = selected.IndexOf("$$x", StringComparison.Ordinal);
        var length = "$$x$\r\n  y$$".Length;
        var wrapped = NoteDisplayMathFormatting.ReplaceSelection(document, start, length, out var blockIndex);

        Assert.AreEqual("left ", Text(wrapped.Paragraphs[0]));
        Assert.AreEqual(NoteBlockKind.DisplayMath, wrapped.Paragraphs[1].Kind);
        Assert.AreEqual("$$x$\r\n  y$$", Text(wrapped.Paragraphs[1]));
        Assert.AreEqual(" right", Text(wrapped.Paragraphs[2]));
        Assert.AreEqual(1, blockIndex);

        const string caretText = "before after";
        var caretDocument = NotesDocument.FromText(caretText);
        var inserted = NoteDisplayMathFormatting.InsertAtCaret(caretDocument, 7, out blockIndex);
        CollectionAssert.AreEqual(new[] { "before ", string.Empty, "after" }, inserted.Paragraphs.Select(Text).ToArray());
        Assert.AreEqual(NoteBlockKind.DisplayMath, inserted.Paragraphs[blockIndex].Kind);

        var empty = NoteDisplayMathFormatting.InsertAtCaret(NotesDocument.FromText(string.Empty), 0, out blockIndex);
        Assert.AreEqual(NoteBlockKind.DisplayMath, empty.Paragraphs[blockIndex].Kind);
        Assert.AreEqual(2, empty.Paragraphs.Count);
        Assert.AreEqual(NoteBlockKind.Paragraph, empty.Paragraphs[1].Kind);
    }

    [TestMethod]
    public void DisplayMathSurvivesNoteHistoryTrashRestoreAndVersionFiveMigration()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hood-notes-display-math-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "notes.json");
            var app = NotesApplication.Start(new JsonNotesStore(path));
            var noteId = app.CreateNote();
            var document = new NotesDocument
            {
                Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.DisplayMath, Runs = [new NoteTextRun("$$\\badcommand$$")] }]
            };
            app.UpdateDocument(noteId, document);
            Assert.IsTrue(app.Undo(noteId));
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            Assert.IsTrue(app.Redo(noteId));
            Assert.IsTrue(app.TryMoveToTrash(noteId, out var trashError), trashError);

            var restarted = NotesApplication.Start(new JsonNotesStore(path));
            Assert.AreEqual(NoteBlockKind.DisplayMath, restarted.Trash.Single().Document.Paragraphs[0].Kind);
            Assert.AreEqual("$$\\badcommand$$", restarted.Trash.Single().Document.GetPlainText());
            Assert.IsTrue(restarted.TryRestoreFromTrash(noteId, out var restoreError), restoreError);
            Assert.AreEqual(NoteBlockKind.DisplayMath,
                NotesApplication.Start(new JsonNotesStore(path)).GetNote(noteId).Document.Paragraphs[0].Kind);

            File.WriteAllText(path, """{"schemaVersion":5,"notes":[],"trash":[],"openNoteIds":[]}""");
            var migrated = new JsonNotesStore(path).Load();
            Assert.AreEqual(NotesSnapshot.CurrentSchemaVersion, migrated!.SchemaVersion);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Text(NoteParagraph paragraph) => string.Concat(paragraph.Runs.Select(run => run.Text));
}
