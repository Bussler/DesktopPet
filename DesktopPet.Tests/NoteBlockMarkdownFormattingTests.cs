using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteBlockMarkdownFormattingTests
{
    [TestMethod]
    [DataRow("$$", NoteBlockKind.DisplayMath)]
    [DataRow("```", NoteBlockKind.Code)]
    [DataRow("  ```", NoteBlockKind.Code)]
    public void OpeningMarkerCreatesEmptyBlockAndMapsCaret(string text, NoteBlockKind kind)
    {
        var source = NotesDocument.FromText(text);
        var start = text.Length;
        var result = NoteBlockMarkdownFormatting.OpenTypedBlock(source, (start - 1, 1), ref start, 0);
        Assert.AreEqual(kind, result.Paragraphs[0].Kind);
        Assert.AreEqual(0, result.Paragraphs[0].Runs.Count);
        Assert.AreEqual(0, start);
        Assert.AreEqual(text, source.GetPlainText());
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Paragraph)]
    [DataRow(NoteBlockKind.Heading1)]
    public void OpeningMathSplitsNeighborTextWithoutLosingStyles(NoteBlockKind kind)
    {
        var source = NotesDocument.FromText("before $$ after").SetStyle(9, 6, NoteTextStyle.Bold, true);
        source.Paragraphs[0].Kind = kind;
        var start = 9;
        var result = NoteBlockMarkdownFormatting.OpenTypedBlock(source, (8, 1), ref start, 0);
        Assert.AreEqual("before \r\n\r\n after", result.GetPlainText());
        Assert.AreEqual(NoteBlockKind.DisplayMath, result.Paragraphs[1].Kind);
        Assert.AreEqual(kind, result.Paragraphs[0].Kind);
        Assert.AreEqual(kind, result.Paragraphs[2].Kind);
        Assert.AreEqual(NoteTextStyle.Bold, result.Paragraphs[2].Runs.Single().Style);
        Assert.AreEqual(9, start);
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    [DataRow(@"\$$")]
    [DataRow("$")]
    [DataRow("``")]
    [DataRow("$$$")]
    [DataRow("prefix ```")]
    [DataRow("$$x$$")]
    [DataRow("$x $$")]
    [DataRow("```cs\r\n$$")]
    [DataRow("$$\r\n```")]
    [DataRow("$$\r\nx\r\n$$")]
    [DataRow("```\r\nx\r\n```")]
    public void OpeningMarkersLeaveIncompleteEscapedAndExistingSourceLiteral(string text)
    {
        var source = NotesDocument.FromText(text);
        var start = text.Length;
        Assert.AreSame(source, NoteBlockMarkdownFormatting.OpenTypedBlock(source, (start - 1, 1), ref start, 0));
        Assert.AreEqual(text.Length, start);
    }

    [TestMethod]
    [DataRow("$$", NoteBlockKind.Code)]
    [DataRow("```", NoteBlockKind.DisplayMath)]
    [DataRow("$$", NoteBlockKind.Bulleted)]
    [DataRow("```", NoteBlockKind.Heading1)]
    public void OpeningMarkersRespectNonParagraphContexts(string text, NoteBlockKind kind)
    {
        var source = NotesDocument.FromText(text);
        source.Paragraphs[0].Kind = kind;
        var start = text.Length;
        Assert.AreSame(source, NoteBlockMarkdownFormatting.OpenTypedBlock(source, (start - 1, 1), ref start, 0));
    }

    [TestMethod]
    public void OpeningMarkersRespectCellsInlineCodeAndSemanticRuns()
    {
        var sources = new[]
        {
            NotesDocument.FromText("$$").SetStyle(0, 2, NoteTextStyle.Code, true),
            new NotesDocument { Paragraphs = [new NoteParagraph { Cell = new NoteTableCell(Guid.NewGuid(), 0, 0), Runs = [new NoteTextRun("$$")] }] },
            new NotesDocument { Paragraphs = [new NoteParagraph { Runs = [new NoteTextRun("$$", Kind: NoteInlineKind.InlineMath)] }] },
            new NotesDocument { Paragraphs = [new NoteParagraph { Runs = [new NoteTextRun("$$", Kind: NoteInlineKind.Link, LinkDestination: "https://example.com")] }] }
        };
        foreach (var source in sources)
        {
            var start = 2;
            Assert.AreSame(source, NoteBlockMarkdownFormatting.OpenTypedBlock(source, (1, 1), ref start, 0));
        }
        var literal = NotesDocument.FromText("$$");
        var caret = 2;
        Assert.AreSame(literal, NoteBlockMarkdownFormatting.OpenTypedBlock(literal, (2, 0), ref caret, 0));
        caret = 1;
        Assert.AreSame(literal, NoteBlockMarkdownFormatting.OpenTypedBlock(literal, (1, 1), ref caret, 1));
    }

    [TestMethod]
    [DataRow("# Title", NoteBlockKind.Heading1)]
    [DataRow("## Title", NoteBlockKind.Heading2)]
    [DataRow("### Title", NoteBlockKind.Heading3)]
    [DataRow("#### Title", NoteBlockKind.Heading4)]
    [DataRow("##### Title", NoteBlockKind.Heading5)]
    [DataRow("###### Title", NoteBlockKind.Heading6)]
    public void ConvertsSupportedHeadingLevelsAndMapsTheCaret(string sourceText, NoteBlockKind expectedKind)
    {
        var source = NotesDocument.FromText(sourceText);
        var start = sourceText.Length;
        var length = 0;

        var result = NoteBlockMarkdownFormatting.Apply(source, (0, sourceText.Length), ref start, ref length);

        Assert.AreEqual("Title", result.GetPlainText());
        Assert.AreEqual(expectedKind, result.Paragraphs[0].Kind);
        Assert.AreEqual("Title", result.Paragraphs[0].Runs.Single().Text);
        Assert.AreEqual(5, start);
        Assert.AreEqual(0, length);
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    public void ConvertsWhenTheHeadingSpaceIsTypedButKeepsOldLiteralAndUnsupportedPrefixes()
    {
        var source = NotesDocument.FromText("# Title");
        var start = 2;
        var length = 0;

        var result = NoteBlockMarkdownFormatting.Apply(source, (1, 1), ref start, ref length);

        Assert.AreEqual(NoteBlockKind.Heading1, result.Paragraphs[0].Kind);
        Assert.AreEqual("Title", result.GetPlainText());
        Assert.AreEqual(0, start);

        var literal = NotesDocument.FromText("# literal later");
        start = literal.GetPlainText().Length;
        length = 0;
        Assert.AreSame(literal, NoteBlockMarkdownFormatting.Apply(literal, (10, 5), ref start, ref length));

        foreach (var text in new[] { "#title", "####### too many", @"\# escaped", "text # incidental" })
        {
            var unsupported = NotesDocument.FromText(text);
            start = text.Length;
            length = 0;
            Assert.AreSame(unsupported,
                NoteBlockMarkdownFormatting.Apply(unsupported, (0, text.Length), ref start, ref length), text);
        }
    }

    [TestMethod]
    public void HeadingConversionPreservesInlineStylesAndPositionsAcrossParagraphs()
    {
        const string sourceText = "before\r\n## title\r\nafter";
        var source = NotesDocument.FromText(sourceText).SetStyle(11, 5, NoteTextStyle.Italic, true);
        var start = 11;
        var length = sourceText.Length - start;

        var result = NoteBlockMarkdownFormatting.Apply(source, (10, 1), ref start, ref length);

        Assert.AreEqual("before\r\ntitle\r\nafter", result.GetPlainText());
        Assert.AreEqual(NoteBlockKind.Heading2, result.Paragraphs[1].Kind);
        Assert.AreEqual(NoteTextStyle.Italic, result.Paragraphs[1].Runs.Single().Style);
        Assert.AreEqual(8, start);
        Assert.AreEqual(result.GetPlainText().Length - start, length);
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    public void DividerNeedsLineCompletionOrACompletePastedLine()
    {
        var unfinished = NotesDocument.FromText("---");
        var start = 3;
        var length = 0;
        Assert.AreSame(unfinished, NoteBlockMarkdownFormatting.Apply(unfinished, (2, 1), ref start, ref length));

        const string enteredText = "before\r\n---\r\nafter";
        var entered = NotesDocument.FromText(enteredText);
        start = 13;
        length = 0;
        var afterEnter = NoteBlockMarkdownFormatting.Apply(entered, (11, 2), ref start, ref length);
        Assert.AreEqual("before\r\n\r\nafter", afterEnter.GetPlainText());
        Assert.AreEqual(NoteBlockKind.Divider, afterEnter.Paragraphs[1].Kind);
        Assert.AreEqual(0, afterEnter.Paragraphs[1].Runs.Count);
        Assert.AreEqual(10, start);
        Assert.AreEqual(NoteBlockKind.Paragraph, afterEnter.Paragraphs[2].Kind);

        var pasted = NotesDocument.FromText("before\r\n---");
        start = pasted.GetPlainText().Length;
        length = 0;
        var pastedDivider = NoteBlockMarkdownFormatting.Apply(pasted, (8, 3), ref start, ref length, pasted: true);
        Assert.AreEqual("before\r\n\r\n", pastedDivider.GetPlainText());
        Assert.AreEqual(NoteBlockKind.Divider, pastedDivider.Paragraphs[1].Kind);
        Assert.AreEqual(NoteBlockKind.Paragraph, pastedDivider.Paragraphs[2].Kind);
        Assert.AreEqual(pastedDivider.GetPlainText().Length, start);
        NotesSnapshotValidator.ValidateDocument(pastedDivider);
    }

    [TestMethod]
    public void BlockSyntaxDoesNotConvertProseEscapesCodeFencesMathOrTables()
    {
        foreach (var text in new[] { "text ---", @"\---", "heading # incidental" })
        {
            var source = NotesDocument.FromText(text);
            var start = text.Length;
            var length = 0;
            Assert.AreSame(source, NoteBlockMarkdownFormatting.Apply(source, (0, text.Length), ref start, ref length));
        }

        var fence = new string((char)96, 3);
        var codeSource = fence + "cs\r\n---\r\n# code\r\n" + fence;
        var code = NotesDocument.FromText(codeSource);
        var startOffset = codeSource.Length;
        var lengthOffset = 0;
        code = NoteCodeFormatting.Apply(code, ref startOffset, ref lengthOffset);
        var formatted = NoteBlockMarkdownFormatting.Apply(code, (0, code.GetPlainText().Length), ref startOffset, ref lengthOffset);
        var codeBlock = formatted.Paragraphs.Single(row => row.Kind == NoteBlockKind.Code);
        Assert.AreEqual("---\r\n# code", string.Concat(codeBlock.Runs.Select(run => run.Text)));

        var unfinishedFence = fence + "cs\r\n---\r\n# literal";
        var unfinished = NotesDocument.FromText(unfinishedFence);
        startOffset = unfinishedFence.Length;
        lengthOffset = 0;
        Assert.AreSame(unfinished, NoteBlockMarkdownFormatting.Apply(
            unfinished, (0, unfinishedFence.Length), ref startOffset, ref lengthOffset));

        const string mathSource = "$$\r\n---\r\n# math\r\n$$";
        var math = NotesDocument.FromText(mathSource);
        startOffset = mathSource.Length;
        lengthOffset = 0;
        Assert.AreSame(math, NoteBlockMarkdownFormatting.Apply(math, (0, mathSource.Length), ref startOffset, ref lengthOffset));

        const string tableSource = "Name|Value\r\n---|---\r\nA|B";
        var table = NotesDocument.FromText(tableSource);
        startOffset = tableSource.Length;
        lengthOffset = 0;
        table = NoteTableMarkdown.Apply(table, true, ref startOffset, ref lengthOffset);
        var tableContent = NoteBlockMarkdownFormatting.Apply(
            table, (0, table.GetPlainText().Length), ref startOffset, ref lengthOffset);
        Assert.IsTrue(tableContent.Paragraphs.All(row => row.Cell is not null));
        Assert.IsFalse(tableContent.Paragraphs.Any(row => row.Kind == NoteBlockKind.Divider));
    }

    [TestMethod]
    public void SemanticBlocksSurviveHistoryTrashRestoreAndRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hood-notes-blocks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "notes.json");
            var app = NotesApplication.Start(new JsonNotesStore(path));
            var noteId = app.CreateNote();
            const string sourceText = "## Heading\r\n---\r\nafter";
            var source = NotesDocument.FromText(sourceText);
            app.UpdateDocument(noteId, source);
            var start = sourceText.Length;
            var length = 0;
            var formatted = NoteBlockMarkdownFormatting.Apply(source, (0, sourceText.Length), ref start, ref length);
            app.UpdateDocument(noteId, formatted);

            Assert.IsTrue(app.Undo(noteId));
            Assert.AreEqual(sourceText, app.GetNote(noteId).Document.GetPlainText());
            Assert.IsTrue(app.Redo(noteId));
            Assert.AreEqual(NoteBlockKind.Heading2, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.Divider, app.GetNote(noteId).Document.Paragraphs[1].Kind);

            Assert.IsTrue(app.TryMoveToTrash(noteId, out var trashError), trashError);
            var restarted = NotesApplication.Start(new JsonNotesStore(path));
            Assert.AreEqual(NoteBlockKind.Heading2, restarted.Trash.Single().Document.Paragraphs[0].Kind);
            Assert.IsTrue(restarted.TryRestoreFromTrash(noteId, out var restoreError), restoreError);

            var restored = NotesApplication.Start(new JsonNotesStore(path)).GetNote(noteId).Document;
            Assert.AreEqual("Heading\r\n\r\nafter", restored.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Heading2, restored.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.Divider, restored.Paragraphs[1].Kind);
            Assert.AreEqual(NoteBlockKind.Paragraph, restored.Paragraphs[2].Kind);
            Assert.AreEqual(restored.GetPlainText(), restored.Copy().GetPlainText());
            NotesSnapshotValidator.ValidateDocument(restored);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ValidatorRejectsBodyBlocksInsideTablesAndTextOnDividers()
    {
        var tableCell = new NoteTableCell(Guid.NewGuid(), 0, 0, NoteCellAlignment.Left);
        var invalidHeading = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Cell = tableCell, Kind = NoteBlockKind.Heading1 }]
        };
        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.ValidateDocument(invalidHeading));

        var invalidDivider = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Divider, Runs = [new NoteTextRun("text")] }]
        };
        Assert.Throws<InvalidDataException>(() => NotesSnapshotValidator.ValidateDocument(invalidDivider));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NotesDocument.FromText("title").SetListKind(0, 5, NoteBlockKind.Heading1));
    }
}