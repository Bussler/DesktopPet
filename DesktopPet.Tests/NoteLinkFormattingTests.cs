using System.IO;
using DesktopPet.Core;

namespace DesktopPet.Tests;

[TestClass]
public sealed class NoteLinkFormattingTests
{
    [TestMethod]
    public void PastedMarkdownLinkKeepsLabelDestinationAndMapsCaret()
    {
        const string source = "before [label](https://example.com/path_(one)?q=a#frag) after";
        var document = NotesDocument.FromText(source);
        var selectionStart = source.Length;
        var selectionLength = 0;

        var formatted = NoteLinkFormatting.Apply(document, (0, source.Length), ref selectionStart,
            ref selectionLength, pasted: true);

        Assert.AreEqual("before label after", formatted.GetPlainText());
        var link = formatted.Paragraphs[0].Runs.Single(run => run.Kind == NoteInlineKind.Link);
        Assert.AreEqual("label", link.Text);
        Assert.AreEqual("https://example.com/path_(one)?q=a#frag", link.LinkDestination);
        Assert.AreEqual(formatted.GetPlainText().Length, selectionStart);
        NotesSnapshotValidator.ValidateDocument(formatted);
    }

    [TestMethod]
    public void TypedMarkdownConvertsOnlyWhenItsClosingParenthesisIsNewInput()
    {
        const string source = "[guide](https://example.com/docs)";
        var document = NotesDocument.FromText(source);
        var selectionStart = source.Length;
        var selectionLength = 0;

        var formatted = NoteLinkFormatting.Apply(document, (source.Length - 1, 1), ref selectionStart,
            ref selectionLength);

        Assert.AreEqual("guide", formatted.GetPlainText());
        Assert.AreEqual("https://example.com/docs", formatted.Paragraphs[0].Runs.Single().LinkDestination);
        Assert.AreEqual(NoteInlineKind.Link, formatted.Paragraphs[0].Runs.Single().Kind);
    }

    [TestMethod]
    public void PlainUrlsWaitForPasteBoundaryOrLeavingTheAddressAndTrimSentencePunctuation()
    {
        const string url = "HTTPS://example.com/a_(b)?q=one#part";
        const string pasted = "See " + url + ".";
        var document = NotesDocument.FromText(pasted);
        var selectionStart = pasted.Length;
        var selectionLength = 0;

        var formatted = NoteLinkFormatting.Apply(document, (0, pasted.Length), ref selectionStart,
            ref selectionLength, pasted: true);

        Assert.AreEqual(pasted, formatted.GetPlainText());
        var link = formatted.Paragraphs[0].Runs.Single(run => run.Kind == NoteInlineKind.Link);
        Assert.AreEqual(url, link.Text);
        Assert.AreEqual(url, link.LinkDestination);
        Assert.AreEqual(".", formatted.Paragraphs[0].Runs.Last().Text);

        var typed = NotesDocument.FromText(url);
        selectionStart = url.Length;
        Assert.AreSame(typed, NoteLinkFormatting.Apply(typed, (url.Length - 1, 1), ref selectionStart,
            ref selectionLength));

        typed = NotesDocument.FromText(url + " ");
        selectionStart = url.Length + 1;
        formatted = NoteLinkFormatting.Apply(typed, (url.Length, 1), ref selectionStart, ref selectionLength,
            forcePlainUrl: (0, url.Length));
        Assert.AreEqual(url, formatted.Paragraphs[0].Runs.Single(run => run.Kind == NoteInlineKind.Link).LinkDestination);

        typed = NotesDocument.FromText(url);
        selectionStart = 0;
        formatted = NoteLinkFormatting.Apply(typed, (0, 0), ref selectionStart, ref selectionLength,
            forcePlainUrl: (0, url.Length));
        Assert.AreEqual(url, formatted.Paragraphs[0].Runs.Single().LinkDestination);
    }

    [TestMethod]
    public void UnsupportedMalformedEscapedAndOldLiteralLinksStayInactive()
    {
        const string source = "[email](mailto:a@example.com) file://example.test https://example.com/%zz \\[literal](https://example.org)";
        var document = NotesDocument.FromText(source);
        var selectionStart = source.Length;
        var selectionLength = 0;
        var unchanged = NoteLinkFormatting.Apply(document, (0, source.Length), ref selectionStart,
            ref selectionLength, pasted: true);

        Assert.AreSame(document, unchanged, string.Join(" | ", unchanged.Paragraphs.SelectMany(row => row.Runs)
            .Select(run => $"{run.Kind}:{run.Text}:{run.LinkDestination}")));
        const string oldLiteral = "keep https://old.example";
        document = NotesDocument.FromText(oldLiteral + "!");
        selectionStart = oldLiteral.Length + 1;
        unchanged = NoteLinkFormatting.Apply(document, (oldLiteral.Length, 1), ref selectionStart,
            ref selectionLength);
        Assert.AreSame(document, unchanged);
    }

    [TestMethod]
    public void LinkRecognitionSkipsCodeFencesAndMathButWorksInTableCells()
    {
        const string url = "https://example.test";
        var code = new NoteParagraph
        {
            Runs = [new NoteTextRun(url, NoteTextStyle.Code)]
        };
        var math = new NoteParagraph
        {
            Runs = [new NoteTextRun("$" + url + "$", NoteTextStyle.None, NoteInlineKind.InlineMath)]
        };
        var table = new NoteParagraph
        {
            Cell = new NoteTableCell(Guid.NewGuid(), 0, 0),
            Runs = [new NoteTextRun(url)]
        };
        var document = new NotesDocument
        {
            Paragraphs = [
                new NoteParagraph { Runs = [new NoteTextRun("```" )] },
                new NoteParagraph { Runs = [new NoteTextRun(url)] },
                new NoteParagraph { Runs = [new NoteTextRun("```")] },
                code, math, table
            ]
        };
        var selectionStart = document.GetPlainText().Length;
        var selectionLength = 0;

        var formatted = NoteLinkFormatting.Apply(document, (0, selectionStart), ref selectionStart,
            ref selectionLength, pasted: true);

        Assert.IsFalse(formatted.Paragraphs.Take(5).SelectMany(row => row.Runs)
            .Any(run => run.Kind == NoteInlineKind.Link));
        Assert.AreEqual(NoteInlineKind.Link, formatted.Paragraphs[5].Runs.Single().Kind);
        Assert.AreEqual(url, formatted.Paragraphs[5].Runs.Single().LinkDestination);
    }

    [TestMethod]
    public void UrlDestinationsMayContainDollarCharactersWithoutBecomingMath()
    {
        const string url = "https://example.com/$x$/price$5?q=$value";
        var document = NotesDocument.FromText("[cost](" + url + ") " + url);
        var selectionStart = document.GetPlainText().Length;
        var selectionLength = 0;

        var formatted = NoteLinkFormatting.Apply(document, (0, selectionStart), ref selectionStart,
            ref selectionLength, pasted: true);

        Assert.AreEqual("cost " + url, formatted.GetPlainText());
        var links = formatted.Paragraphs[0].Runs.Where(run => run.Kind == NoteInlineKind.Link).ToArray();
        Assert.AreEqual(2, links.Length);
        Assert.IsTrue(links.All(run => run.LinkDestination == url));

        var mathSelectionStart = formatted.GetPlainText().Length;
        var withMath = NoteInlineMathFormatting.Apply(formatted, (0, mathSelectionStart),
            ref mathSelectionStart, ref selectionLength);
        Assert.AreEqual(2, withMath.Paragraphs[0].Runs.Count(run => run.Kind == NoteInlineKind.Link));
        Assert.IsFalse(withMath.Paragraphs[0].Runs.Any(run => run.Kind == NoteInlineKind.InlineMath));
    }

    [TestMethod]
    public void EditAndRemoveLinkPreserveOrdinaryStylesAndValidateDestinations()
    {
        const string destination = "https://example.com";
        var document = new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Runs = [
                    new NoteTextRun("Bold", NoteTextStyle.Bold, NoteInlineKind.Link, destination),
                    new NoteTextRun(" label", NoteTextStyle.Italic, NoteInlineKind.Link, destination)
                ]
            }]
        };

        var edited = NoteLinkFormatting.UpdateLink(document, 0, 10, "New label", "HTTPS://new.example/path?q=1#f");
        Assert.AreEqual("New label", edited.GetPlainText());
        Assert.AreEqual(NoteTextStyle.Bold, edited.Paragraphs[0].Runs.Single().Style);
        Assert.AreEqual("HTTPS://new.example/path?q=1#f", edited.Paragraphs[0].Runs.Single().LinkDestination);

        var removed = NoteLinkFormatting.RemoveLink(document, 0, 10);
        Assert.AreEqual("Bold label", removed.GetPlainText());
        CollectionAssert.AreEqual(new[] { NoteTextStyle.Bold, NoteTextStyle.Italic },
            removed.Paragraphs[0].Runs.Select(run => run.Style).ToArray());
        Assert.IsTrue(removed.Paragraphs[0].Runs.All(run => run.Kind == NoteInlineKind.Text && run.LinkDestination is null));
        Assert.Throws<ArgumentException>(() =>
            NoteLinkFormatting.UpdateLink(document, 0, 10, "label", "javascript:alert(1)"));
    }

    [TestMethod]
    public void LinkSemanticsSurviveOtherInlineFormattingAndSchemaSixMigration()
    {
        const string destination = "https://example.test";
        var document = new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Runs = [new NoteTextRun("link `code` **bold** $x$", NoteTextStyle.None)]
            }]
        };
        var start = document.GetPlainText().Length;
        var length = 0;
        var linked = NoteLinkFormatting.Apply(document, (0, start), ref start, ref length, pasted: true);
        Assert.AreEqual("link `code` **bold** $x$", linked.GetPlainText());

        var mixed = new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Runs = [
                    new NoteTextRun("link", NoteTextStyle.Bold, NoteInlineKind.Link, destination),
                    new NoteTextRun(" `code` **bold** $x$")
                ]
            }]
        };
        start = mixed.GetPlainText().IndexOf("`code`", StringComparison.Ordinal);
        var formatted = NoteCodeFormatting.Apply(mixed, ref start, ref length, (start, 6));
        Assert.AreEqual(NoteInlineKind.Link, formatted.Paragraphs[0].Runs[0].Kind);
        Assert.AreEqual(destination, formatted.Paragraphs[0].Runs[0].LinkDestination);

        var emphasisStart = formatted.GetPlainText().IndexOf("**bold**", StringComparison.Ordinal);
        formatted = NoteEmphasisFormatting.Apply(formatted, (emphasisStart, 8), ref emphasisStart, ref length);
        Assert.IsTrue(formatted.Paragraphs[0].Runs.Any(run => run.Text == "bold" &&
            run.Style.HasFlag(NoteTextStyle.Bold)));
        Assert.AreEqual(destination, formatted.Paragraphs[0].Runs[0].LinkDestination);

        var mathStart = formatted.GetPlainText().IndexOf("$x$", StringComparison.Ordinal);
        formatted = NoteInlineMathFormatting.Apply(formatted, (mathStart, 3), ref mathStart, ref length);
        Assert.IsTrue(formatted.Paragraphs[0].Runs.Any(run => run.Kind == NoteInlineKind.InlineMath && run.Text == "$x$"));
        Assert.AreEqual(destination, formatted.Paragraphs[0].Runs[0].LinkDestination);

        var directory = Path.Combine(Path.GetTempPath(), $"hood-notes-link-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "notes.json");
            var app = NotesApplication.Start(new JsonNotesStore(path));
            var id = app.CreateNote();
            app.UpdateDocument(id, new NotesDocument
            {
                Paragraphs = [new NoteParagraph
                {
                    Runs = [new NoteTextRun("label", NoteTextStyle.Underline, NoteInlineKind.Link, destination)]
                }]
            });
            var loaded = NotesApplication.Start(new JsonNotesStore(path)).GetNote(id).Document;
            Assert.AreEqual(NoteInlineKind.Link, loaded.Paragraphs[0].Runs.Single().Kind);
            Assert.AreEqual(destination, loaded.Paragraphs[0].Runs.Single().LinkDestination);

            Assert.IsTrue(app.TryMoveToTrash(id, out var trashError), trashError);
            var restarted = NotesApplication.Start(new JsonNotesStore(path));
            Assert.AreEqual(destination, restarted.Trash.Single().Document.Paragraphs[0].Runs.Single().LinkDestination);
            Assert.IsTrue(restarted.TryRestoreFromTrash(id, out var restoreError), restoreError);
            var restored = NotesApplication.Start(new JsonNotesStore(path)).GetNote(id).Document;
            Assert.AreEqual(NoteInlineKind.Link, restored.Paragraphs[0].Runs.Single().Kind);
            Assert.AreEqual(destination, restored.Paragraphs[0].Runs.Single().LinkDestination);

            File.WriteAllText(path, "{\"schemaVersion\":6,\"notes\":[],\"trash\":[],\"openNoteIds\":[]}");
            Assert.AreEqual(NotesSnapshot.CurrentSchemaVersion, new JsonNotesStore(path).Load()!.SchemaVersion);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
