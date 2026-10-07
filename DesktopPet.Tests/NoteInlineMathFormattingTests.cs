using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteInlineMathFormattingTests
{
    [TestMethod]
    public void CompletedAndPastedInlineMathRetainsExactDelimitedSourceAndPosition()
    {
        const string text = "Before $\\frac{1}{2}$, then $\\sqrt{x_1^2}$ after.";
        var document = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;

        var formatted = NoteInlineMathFormatting.Apply(document, (0, text.Length), ref start, ref length);

        Assert.AreEqual(text, formatted.GetPlainText());
        Assert.AreEqual(text.Length, start);
        Assert.AreEqual(0, length);
        CollectionAssert.AreEqual(
            new[] { "$\\frac{1}{2}$", "$\\sqrt{x_1^2}$" },
            formatted.Paragraphs[0].Runs.Where(run => run.Kind == NoteInlineKind.InlineMath)
                .Select(run => run.Text).ToArray());
        Assert.AreEqual(NoteInlineKind.Text, formatted.Paragraphs[0].Runs[0].Kind);
    }

    [TestMethod]
    public void TypingClosingDollarCompletesOnlyNewInput()
    {
        var unfinished = NotesDocument.FromText("before $x");
        var start = unfinished.GetPlainText().Length;
        var length = 0;
        var stillLiteral = NoteInlineMathFormatting.Apply(
            unfinished, (7, 2), ref start, ref length);
        Assert.AreSame(unfinished, stillLiteral);

        var completed = NotesDocument.FromText("before $x$");
        start = completed.GetPlainText().Length;
        var formatted = NoteInlineMathFormatting.Apply(completed, (9, 1), ref start, ref length);
        Assert.AreEqual("before $x$", formatted.GetPlainText());
        Assert.AreEqual("$x$", formatted.Paragraphs[0].Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);

        var oldLiteral = NotesDocument.FromText("$x$");
        start = 3;
        Assert.AreSame(oldLiteral, NoteInlineMathFormatting.Apply(oldLiteral, (0, 0), ref start, ref length));

        const string oldUnfinished = "legacy $ text ";
        const string pastedFormula = "$x$";
        var adjacent = NotesDocument.FromText(oldUnfinished + pastedFormula);
        start = oldUnfinished.Length + pastedFormula.Length;
        var pasted = NoteInlineMathFormatting.Apply(
            adjacent, (oldUnfinished.Length, pastedFormula.Length), ref start, ref length);
        CollectionAssert.AreEqual(new[] { pastedFormula }, pasted.Paragraphs[0].Runs
            .Where(run => run.Kind == NoteInlineKind.InlineMath).Select(run => run.Text).ToArray());
    }

    [TestMethod]
    public void EscapedDollarsDisplayMathCodeAndFencesRemainLiteral()
    {
        const string fence = "\u0060\u0060\u0060";
        var text = "Use \\$5, $$a+b$$, and $z$.\r\n" + fence + "\r\n$q$\r\n" + fence;
        var document = NotesDocument.FromText(text);
        var start = text.Length;
        var length = 0;

        var formatted = NoteInlineMathFormatting.Apply(document, (0, text.Length), ref start, ref length);

        Assert.AreEqual(text, formatted.GetPlainText());
        CollectionAssert.AreEqual(new[] { "$z$" },
            formatted.Paragraphs.SelectMany(row => row.Runs)
                .Where(run => run.Kind == NoteInlineKind.InlineMath).Select(run => run.Text).ToArray());

        var code = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Runs = [new NoteTextRun("$literal$", NoteTextStyle.Code)] }]
        };
        start = code.GetPlainText().Length;
        Assert.AreSame(code, NoteInlineMathFormatting.Apply(code, (0, start), ref start, ref length));
    }

    [TestMethod]
    public void ExistingMathSourceIsProtectedFromLaterFormattingAndKeepsItsKindWhenSliced()
    {
        var source = new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Runs =
                [
                    new NoteTextRun("$unfinished*source", NoteTextStyle.None, NoteInlineKind.InlineMath),
                    new NoteTextRun(" and *emphasis*")
                ]
            }]
        };
        var text = source.GetPlainText();
        var start = text.Length;
        var length = 0;
        Assert.AreSame(source, NoteInlineMathFormatting.Apply(source, (text.Length - 8, 8), ref start, ref length));

        var emphasized = NoteEmphasisFormatting.Apply(source, (text.Length - 10, 10), ref start, ref length);
        Assert.AreEqual("$unfinished*source and emphasis", emphasized.GetPlainText());
        Assert.AreEqual(NoteInlineKind.InlineMath, emphasized.Paragraphs[0].Runs[0].Kind);
        Assert.IsTrue(emphasized.Paragraphs[0].Runs.Any(run => run.Style.HasFlag(NoteTextStyle.Italic)));

        var styled = source.SetStyle(1, 3, NoteTextStyle.Bold, enabled: true);
        Assert.AreEqual(text, styled.GetPlainText());
        Assert.AreEqual("$unfinished*source".Length,
            styled.Paragraphs[0].Runs.Where(run => run.Kind == NoteInlineKind.InlineMath)
                .Sum(run => run.Text.Length));
        var split = source.SplitParagraph(0, 4);
        Assert.AreEqual(NoteInlineKind.InlineMath, split.Paragraphs[0].Runs.Single().Kind);
        Assert.AreEqual(NoteInlineKind.InlineMath, split.Paragraphs[1].Runs[0].Kind);
    }

    [TestMethod]
    public void InlineMathConvertsInsideTableCellsAndPreservesCellIdentity()
    {
        var tableId = Guid.NewGuid();
        var cell = new NoteTableCell(tableId, 0, 1, NoteCellAlignment.Center);
        var source = new NotesDocument
        {
            Paragraphs = [
                new NoteParagraph { Cell = new NoteTableCell(tableId, 0, 0) },
                new NoteParagraph { Cell = cell, Runs = [new NoteTextRun("value $x^2$")] }
            ]
        };
        var selectionStart = source.GetPlainText().Length;
        var selectionLength = 0;

        var formatted = NoteInlineMathFormatting.Apply(source, (0, selectionStart), ref selectionStart,
            ref selectionLength);

        Assert.AreEqual(source.GetPlainText(), formatted.GetPlainText());
        Assert.AreEqual(cell, formatted.Paragraphs[1].Cell);
        Assert.AreEqual("$x^2$", formatted.Paragraphs[1].Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);
        NotesSnapshotValidator.ValidateDocument(formatted);
    }
}
