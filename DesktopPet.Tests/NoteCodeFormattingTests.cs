using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteCodeFormattingTests
{
    [TestMethod]
    public void InlineCodeKeepsOtherFormattingAndMapsSelectionAcrossSeveralSpans()
    {
        var original = NotesDocument.FromText("Use `one` and `two`.").SetStyle(5, 3, NoteTextStyle.Bold, true);
        var start = 5;
        var length = 12;
        var result = NoteCodeFormatting.Apply(original, ref start, ref length);

        Assert.AreEqual("Use one and two.", result.GetPlainText());
        Assert.AreEqual(4, start);
        Assert.AreEqual(10, length);
        Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Code, result.Paragraphs[0].Runs.Single(run => run.Text == "one").Style);
        Assert.AreEqual(NoteTextStyle.Code, result.Paragraphs[0].Runs.Single(run => run.Text == "two").Style);
        Assert.AreEqual("Use `one` and `two`.", original.GetPlainText());
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    [DataRow("before\r\n```\r\n  a\r\n\r\n\tb `literal`\r\n```\r\nafter", "before\r\n  a\r\n\r\n\tb `literal`\r\nafter", 1)]
    [DataRow("```cs\r\n  a\r\n```", "  a\r\n", 0)]
    [DataRow("```\r\n```", "\r\n", 0)]
    public void FencedCodeKeepsIndentationBlankLinesAndLiteralBackticks(string source, string expected, int codeIndex)
    {
        var start = source.Length;
        var length = 0;
        var result = NoteCodeFormatting.Apply(NotesDocument.FromText(source), ref start, ref length);

        Assert.AreEqual(expected, result.GetPlainText());
        Assert.AreEqual(expected.Length, start);
        Assert.AreEqual(NoteBlockKind.Code, result.Paragraphs[codeIndex].Kind);
        Assert.AreEqual(1, result.Paragraphs.Count(row => row.Kind == NoteBlockKind.Code));
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    [DataRow("```\r\ncode\r\n```", 2)]
    [DataRow("```\r\ncode\r\n```\r\nafter", 2)]
    [DataRow("```\r\ncode\r\n```\r\n```\r\nother\r\n```", 3)]
    public void ClosingFenceMovesCaretToOrdinaryTextWithoutLosingFollowingBlocks(string source, int expectedCount)
    {
        var start = "```\r\ncode\r\n```".Length;
        var length = 0;
        var result = NoteCodeFormatting.Apply(NotesDocument.FromText(source), ref start, ref length);
        Assert.AreEqual(expectedCount, result.Paragraphs.Count);
        Assert.AreEqual(6, start);
        Assert.AreEqual(NoteBlockKind.Paragraph, result.Paragraphs[1].Kind);
        Assert.AreEqual(0, length);
        if (source.EndsWith("after", StringComparison.Ordinal))
            Assert.AreEqual("code\r\nafter", result.GetPlainText());
        if (source.Contains("other", StringComparison.Ordinal))
            Assert.AreEqual(NoteBlockKind.Code, result.Paragraphs[2].Kind);
        NotesSnapshotValidator.ValidateDocument(result);
    }

    [TestMethod]
    [DataRow("unfinished `code")]
    [DataRow("`first\r\nsecond`")]
    [DataRow("``not single``")]
    [DataRow("```\r\n`literal` inside unfinished fence")]
    [DataRow("\\`literal\\`")]
    public void IncompleteEscapedAndUnsupportedMarkersRemainLiteral(string text)
    {
        var source = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;
        Assert.AreSame(source, NoteCodeFormatting.Apply(source, ref start, ref length));
        Assert.AreEqual(text.Length, start);
    }

    [TestMethod]
    public void ExistingCodeIsNotParsedAgainAndCodeInsideListsPreservesTheList()
    {
        var source = NotesDocument.FromText("`literal`").SetStyle(0, 9, NoteTextStyle.Code, true);
        var start = 9;
        var length = 0;
        Assert.AreSame(source, NoteCodeFormatting.Apply(source, ref start, ref length));
        source = NotesDocument.FromText("Use `code`").SetListKind(0, 10, NoteBlockKind.Checklist);
        source = source.SetChecklistChecked(0, true);
        start = 10;
        var result = NoteCodeFormatting.Apply(source, ref start, ref length);
        Assert.AreEqual("Use code", result.GetPlainText());
        Assert.AreEqual(NoteBlockKind.Checklist, result.Paragraphs[0].Kind);
        Assert.IsTrue(result.Paragraphs[0].IsChecked);
    }

    [TestMethod]
    public void InlineAndDisplayMathSourceIsProtectedFromInlineCodeConversion()
    {
        const char marker = '\x60';
        var sourceText = "$" + marker + "literal" + marker + "$ and " + marker + "ordinary" + marker;
        var source = NotesDocument.FromText(sourceText);
        var start = sourceText.Length;
        var length = 0;

        var result = NoteCodeFormatting.Apply(source, ref start, ref length);

        Assert.AreEqual("$" + marker + "literal" + marker + "$ and ordinary", result.GetPlainText());
        Assert.AreEqual(NoteTextStyle.None, result.Paragraphs[0].Runs
            .Single(run => run.Text.Contains("literal", StringComparison.Ordinal)).Style);
        Assert.AreEqual(NoteTextStyle.Code, result.Paragraphs[0].Runs.Single(run => run.Text == "ordinary").Style);

        var displayText = "$$\r\n" + marker + "literal" + marker + "\r\n$$\r\n" +
                          marker + "ordinary" + marker;
        var display = NotesDocument.FromText(displayText);
        start = display.GetPlainText().Length;
        result = NoteCodeFormatting.Apply(display, ref start, ref length);
        Assert.AreEqual("$$\r\n" + marker + "literal" + marker + "\r\n$$\r\nordinary", result.GetPlainText());
        Assert.AreEqual(NoteTextStyle.None, result.Paragraphs[1].Runs.Single().Style);
        Assert.AreEqual(NoteTextStyle.Code, result.Paragraphs[^1].Runs.Single().Style);
    }

    [TestMethod]
    public void CaretInsideFenceOrInlineMarkersStaysWithinTheConvertedDocument()
    {
        foreach (var text in new[] { "a `code` z", "before\r\n```\r\none\r\ntwo\r\n```\r\nafter", "```\r\n```" })
        {
            for (var offset = 0; offset <= text.Length; offset++)
            {
                var start = offset;
                var length = 0;
                var result = NoteCodeFormatting.Apply(NotesDocument.FromText(text), ref start, ref length);
                Assert.IsTrue(start >= 0 && start <= result.GetPlainText().Length, $"Caret {offset} in {text}");
                Assert.AreEqual(0, length);
            }
        }
    }
}
