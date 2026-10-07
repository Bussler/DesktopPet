using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteEmphasisFormattingTests
{
    [TestMethod]
    [DataRow("**bold**", "bold", NoteTextStyle.Bold)]
    [DataRow("*italic*", "italic", NoteTextStyle.Italic)]
    [DataRow("__underlined__", "underlined", NoteTextStyle.Underline)]
    [DataRow("~~removed~~", "removed", NoteTextStyle.Strikethrough)]
    [DataRow("***both***", "both", NoteTextStyle.Bold | NoteTextStyle.Italic)]
    public void ConvertsSupportedMarkersAndMapsTheCaret(string sourceText, string expectedText, NoteTextStyle expectedStyle)
    {
        var source = NotesDocument.FromText(sourceText);
        var start = sourceText.Length;
        var length = 0;
        var result = NoteEmphasisFormatting.Apply(source, (0, sourceText.Length), ref start, ref length);

        Assert.AreEqual(expectedText, result.GetPlainText());
        Assert.AreEqual(expectedText.Length, start);
        Assert.AreEqual(0, length);
        Assert.AreEqual(expectedStyle, result.Paragraphs[0].Runs.Single().Style);
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    public void NestedStylesCombineWithManualFormattingAndConvertSeveralSpans()
    {
        const string sourceText = "Use **bold __under__** and *again*.";
        var source = NotesDocument.FromText(sourceText).SetStyle(6, 4, NoteTextStyle.Italic, true);
        var start = sourceText.Length;
        var length = 0;

        var result = NoteEmphasisFormatting.Apply(source, (0, sourceText.Length), ref start, ref length);

        Assert.AreEqual("Use bold under and again.", result.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic,
            result.Paragraphs[0].Runs.Single(run => run.Text == "bold").Style);
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Underline,
            result.Paragraphs[0].Runs.Single(run => run.Text == "under").Style);
        Assert.AreEqual(NoteTextStyle.Italic,
            result.Paragraphs[0].Runs.Single(run => run.Text == "again").Style);
        Assert.AreEqual(result.GetPlainText().Length, start);
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    public void AdjacentAsterisksCloseNestedStylesInOrder()
    {
        const string sourceText = "*italic **bold*** and **bold *italic***";
        var source = NotesDocument.FromText(sourceText);
        var start = sourceText.Length;
        var length = 0;
        var result = NoteEmphasisFormatting.Apply(source, (0, sourceText.Length), ref start, ref length);

        Assert.AreEqual("italic bold and bold italic", result.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Italic, result.Paragraphs[0].Runs.Single(run => run.Text == "italic ").Style);
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic, result.Paragraphs[0].Runs.Single(run => run.Text == "bold").Style);
        Assert.AreEqual(NoteTextStyle.None, result.Paragraphs[0].Runs.Single(run => run.Text == " and ").Style);
        Assert.AreEqual(NoteTextStyle.Bold, result.Paragraphs[0].Runs.Single(run => run.Text == "bold ").Style);
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic, result.Paragraphs[0].Runs.Single(run => run.Text == "italic").Style);
        NotesSnapshotValidator.ValidateDocument(result);
    }
    [TestMethod]
    public void OnlyNewlyCompletedClosingMarkersConvert()
    {
        var sourceText = "**bold**";
        var source = NotesDocument.FromText(sourceText);
        var start = sourceText.Length;
        var length = 0;
        var unchanged = NoteEmphasisFormatting.Apply(source, (0, 0), ref start, ref length);
        Assert.AreSame(source, unchanged);

        start = sourceText.Length;
        length = 0;
        var converted = NoteEmphasisFormatting.Apply(source, (7, 1), ref start, ref length);
        Assert.AreEqual("bold", converted.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Bold, converted.Paragraphs[0].Runs.Single().Style);
        Assert.AreEqual(4, start);

        var completedByOpening = NotesDocument.FromText("**bold**");
        start = 2;
        length = 0;
        completedByOpening = NoteEmphasisFormatting.Apply(completedByOpening, (0, 2), ref start, ref length);
        Assert.AreEqual("bold", completedByOpening.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Bold, completedByOpening.Paragraphs[0].Runs.Single().Style);

        var laterEdit = NotesDocument.FromText("**bold** more");
        start = laterEdit.GetPlainText().Length;
        length = 0;
        var stillLiteral = NoteEmphasisFormatting.Apply(laterEdit, (8, 5), ref start, ref length);
        Assert.AreSame(laterEdit, stillLiteral);
        Assert.AreEqual("**bold** more", stillLiteral.GetPlainText());
    }

    [TestMethod]
    public void ConvertsNewEmphasisInLaterParagraphsUsingDocumentOffsets()
    {
        const string sourceText = "Existing note\r\n**bold** and *italic*";
        var source = NotesDocument.FromText(sourceText);
        var paragraphStart = source.Paragraphs[0].Runs.Single().Text.Length + 2;
        var start = source.GetPlainText().Length;
        var length = 0;

        var result = NoteEmphasisFormatting.Apply(source,
            (paragraphStart, source.GetPlainText().Length - paragraphStart), ref start, ref length);

        Assert.AreEqual("Existing note\r\nbold and italic", result.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Bold, result.Paragraphs[1].Runs.Single(run => run.Text == "bold").Style);
        Assert.AreEqual(NoteTextStyle.Italic, result.Paragraphs[1].Runs.Single(run => run.Text == "italic").Style);
        Assert.AreEqual(result.GetPlainText().Length, start);
        Assert.AreEqual(0, length);
    }
    [TestMethod]
    public void EscapedEmptyAndUnmatchedMarkersRemainLiteral()
    {
        foreach (var text in new[] { @"\*literal\*", @"\**literal**", @"\__literal__", @"\~~literal~~", "**", "****", "**unfinished", "plain _single_" })
        {
            var source = NotesDocument.FromText(text);
            var start = text.Length;
            var length = 0;
            var result = NoteEmphasisFormatting.Apply(source, (0, text.Length), ref start, ref length);

            Assert.AreSame(source, result, text);
            Assert.AreEqual(text, result.GetPlainText(), text);
            Assert.AreEqual(text.Length, start, text);
        }
    }

    [TestMethod]
    public void CodeAndMathSourceAreProtectedWhileOtherSpansCanConvert()
    {
        var backtick = (char)96;
        var codeSource = backtick + "**literal**" + backtick;
        var code = NotesDocument.FromText(codeSource);
        var start = 0;
        var length = 0;
        var codeDocument = NoteCodeFormatting.Apply(code, ref start, ref length);
        var result = NoteEmphasisFormatting.Apply(codeDocument, (0, codeDocument.GetPlainText().Length), ref start, ref length);

        Assert.AreEqual("**literal**", result.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Code, result.Paragraphs[0].Runs.Single().Style);

        const string mathSource = "a $x **literal**$ and **styled**";
        var math = NotesDocument.FromText(mathSource);
        start = mathSource.Length;
        length = 0;
        result = NoteEmphasisFormatting.Apply(math, (0, mathSource.Length), ref start, ref length);
        Assert.AreEqual("a $x **literal**$ and styled", result.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Bold, result.Paragraphs[0].Runs.Single(run => run.Text == "styled").Style);

        var unfinishedFence = new string(backtick, 3) + "cs\r\n**literal**";
        var fenced = NotesDocument.FromText(unfinishedFence);
        start = fenced.GetPlainText().Length;
        length = 0;
        result = NoteEmphasisFormatting.Apply(fenced, (0, unfinishedFence.Length), ref start, ref length);
        Assert.AreSame(fenced, result);
        Assert.AreEqual(unfinishedFence, result.GetPlainText());
    }

    [TestMethod]
    public void EmphasisSurvivesUndoRedoAndRealStoreRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hood-notes-emphasis-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "notes.json");
            var store = new JsonNotesStore(path);
            var app = NotesApplication.Start(store);
            var id = app.CreateNote();
            const string sourceText = "first **bold** and __underlined__";
            var source = NotesDocument.FromText(sourceText);
            app.UpdateDocument(id, source);
            var start = sourceText.Length;
            var length = 0;
            var formatted = NoteEmphasisFormatting.Apply(source, (0, sourceText.Length), ref start, ref length);
            app.UpdateDocument(id, formatted);

            Assert.IsTrue(app.Undo(id));
            Assert.AreEqual(sourceText, app.GetNote(id).Document.GetPlainText());
            Assert.IsTrue(app.Redo(id));

            var restarted = NotesApplication.Start(new JsonNotesStore(path));
            var document = restarted.GetNote(id).Document;
            Assert.AreEqual("first bold and underlined", document.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold, document.Paragraphs[0].Runs.Single(run => run.Text == "bold").Style);
            Assert.AreEqual(NoteTextStyle.Underline, document.Paragraphs[0].Runs.Single(run => run.Text == "underlined").Style);
            Assert.IsFalse(restarted.HasUnsavedChanges);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    [TestMethod]
    public void MapsSelectionAcrossParagraphBreaks()
    {
        const string sourceText = "left **bold**\r\nright *it*";
        var source = NotesDocument.FromText(sourceText);
        var start = 5;
        var length = sourceText.Length - start;
        var result = NoteEmphasisFormatting.Apply(source, (0, sourceText.Length), ref start, ref length);

        Assert.AreEqual("left bold\r\nright it", result.GetPlainText());
        Assert.AreEqual(5, start);
        Assert.AreEqual(result.GetPlainText().Length - start, length);
        Assert.AreEqual(NoteTextStyle.Bold, result.Paragraphs[0].Runs.Single(run => run.Text == "bold").Style);
        Assert.AreEqual(NoteTextStyle.Italic, result.Paragraphs[1].Runs.Single(run => run.Text == "it").Style);
        NotesSnapshotValidator.ValidateDocument(result);
    }
}