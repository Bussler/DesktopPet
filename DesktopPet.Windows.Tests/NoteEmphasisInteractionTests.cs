using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;
using static DesktopPet.Windows.Tests.NoteClipboardTestInput;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteEmphasisInteractionTests
{
    [TestMethod]
    public void TypedEmphasisUpdatesTheToolbarAndUndoRedoWithoutReconversion()
    {
        const string source = "**bold __under__**";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, source);
            Pump(window);

            var formatted = app.GetNote(noteId).Document;
            Assert.AreEqual("bold under", formatted.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold, formatted.Paragraphs[0].Runs.Single(run => run.Text == "bold ").Style);
            Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Underline,
                formatted.Paragraphs[0].Runs.Single(run => run.Text == "under").Style);

            var under = editor.Document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>()
                .Single(run => run.Text == "under");
            editor.Selection.Select(under.ContentStart, under.ContentEnd);
            Pump(window);
            Assert.AreEqual(true, ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("BoldButton")).IsChecked);
            Assert.AreEqual(true, ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("UnderlineButton")).IsChecked);

            RestoreHistory(window, undo: true);
            Pump(window);
            Assert.AreEqual(source, app.GetNote(noteId).Document.GetPlainText());
            Assert.IsTrue(app.GetNote(noteId).Document.Paragraphs[0].Runs.All(run => run.Style == NoteTextStyle.None));

            RestoreHistory(window, undo: false);
            Pump(window);
            Assert.AreEqual("bold under", app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Underline,
                app.GetNote(noteId).Document.Paragraphs[0].Runs.Single(run => run.Text == "under").Style);

            RestoreHistory(window, undo: true);
            Pump(window);
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, " later");
            Pump(window);
            var later = app.GetNote(noteId).Document;
            Assert.AreEqual(source + " later", later.GetPlainText());
            Assert.IsTrue(later.Paragraphs[0].Runs.All(run => run.Style == NoteTextStyle.None));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void TypingAfterEmphasisConversionDoesNotInheritTheMarkerStyle()
    {
        const string source = "**bold** *italic* __under__ ~~struck~~";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, source);
            Pump(window);
            Type(editor, " plain");
            Pump(window);

            var document = app.GetNote(noteId).Document;
            Assert.AreEqual("bold italic under struck plain", document.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold, document.Paragraphs[0].Runs.Single(run => run.Text == "bold").Style);
            Assert.AreEqual(NoteTextStyle.Italic, document.Paragraphs[0].Runs.Single(run => run.Text == "italic").Style);
            Assert.AreEqual(NoteTextStyle.Underline, document.Paragraphs[0].Runs.Single(run => run.Text == "under").Style);
            Assert.AreEqual(NoteTextStyle.Strikethrough, document.Paragraphs[0].Runs.Single(run => run.Text == "struck").Style);
            Assert.AreEqual(NoteTextStyle.None, document.Paragraphs[0].Runs.Single(run => run.Text == " plain").Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }
    [TestMethod]
    public void ConvertedStylesCanBeRemovedThroughTheExistingToolbar()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, "**bold**");
            Pump(window);

            var boldRun = editor.Document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>()
                .Single(run => run.Text == "bold");
            editor.Selection.Select(boldRun.ContentStart, boldRun.ContentEnd);
            Pump(window);
            var button = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("BoldButton");
            Assert.AreEqual(true, button.IsChecked);

            button.IsChecked = false;
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            Pump(window);

            Assert.AreEqual(NoteTextStyle.None,
                app.GetNote(noteId).Document.Paragraphs[0].Runs.Single(run => run.Text == "bold").Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("BoldButton", NoteTextStyle.Bold)]
    [DataRow("ItalicButton", NoteTextStyle.Italic)]
    [DataRow("UnderlineButton", NoteTextStyle.Underline)]
    [DataRow("StrikethroughButton", NoteTextStyle.Strikethrough)]
    public void OrdinarySelectedFormattingKeepsTheEditorDocumentAndRoundTrips(
        string buttonName,
        NoteTextStyle style)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"NoteFormatting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var storePath = Path.Combine(directory, "notes.json");
        try
        {
            var app = NotesApplication.Start(new JsonNotesStore(storePath));
            var noteId = app.CreateNote();
            var existing = (NoteTextStyle.Bold | NoteTextStyle.Italic | NoteTextStyle.Underline | NoteTextStyle.Strikethrough) & ~style;
            app.UpdateDocument(noteId, new NotesDocument
            {
                Paragraphs = [new NoteParagraph
                {
                    Runs = [new NoteTextRun("before "), new NoteTextRun("untargeted", existing), new NoteTextRun(" after")]
                }]
            });
            var window = CreateWindow(app, noteId);
            try
            {
                ShowHidden(window);
                var editor = (RichTextBox)window.FindName("Editor");
                Assert.IsTrue(editor.Focus());
                var target = editor.Document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>()
                    .Single(run => run.Text == "untargeted");
                var selectedStart = target.ContentStart.GetPositionAtOffset(2)!;
                var selectedEnd = selectedStart.GetPositionAtOffset(6)!;
                editor.Selection.Select(selectedStart, selectedEnd);
                Pump(window);
                var originalDocument = editor.Document;
                var originalSelection = SelectionOffsets(window, app.GetNote(noteId).Document);
                var button = (System.Windows.Controls.Primitives.ToggleButton)window.FindName(buttonName);

                Click(button);
                Pump(window);

                Assert.AreSame(originalDocument, editor.Document,
                    "Ordinary inline formatting must not replace unrelated FlowDocument content.");
                var formatted = app.GetNote(noteId).Document;
                Assert.AreEqual("before untargeted after", formatted.GetPlainText());
                Assert.AreEqual(existing | style, formatted.Paragraphs[0].Runs.Single(run => run.Text == "target").Style);
                Assert.AreEqual(2, formatted.Paragraphs[0].Runs.Count(run => run.Text is "un" or "ed"));
                Assert.IsTrue(formatted.Paragraphs[0].Runs.Where(run => run.Text is "un" or "ed")
                    .All(run => run.Style == existing));
                Assert.AreEqual(NoteTextStyle.None, formatted.Paragraphs[0].Runs.Single(run => run.Text == "before ").Style);
                Assert.AreEqual(NoteTextStyle.None, formatted.Paragraphs[0].Runs.Single(run => run.Text == " after").Style);
                Assert.AreEqual(originalSelection, SelectionOffsets(window, formatted));
                Assert.AreEqual(existing | style,
                    NotesApplication.Start(new JsonNotesStore(storePath)).GetNote(noteId).Document.Paragraphs[0]
                        .Runs.Single(run => run.Text == "target").Style);

                RestoreHistory(window, undo: true);
                Pump(window);
                Assert.AreEqual(existing,
                    app.GetNote(noteId).Document.Paragraphs[0].Runs.Single(run => run.Text == "untargeted").Style);
                RestoreHistory(window, undo: false);
                Pump(window);
                Assert.AreEqual(existing | style,
                    app.GetNote(noteId).Document.Paragraphs[0].Runs.Single(run => run.Text == "target").Style);
            }
            finally { window.CloseWithoutMembershipChange(); }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void MultilineSelectionFormatsOnlyItsTextWithoutReplacingTheDocument()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, NotesDocument.FromText("first line\r\nsecond line"));
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var paragraphs = editor.Document.Blocks.OfType<Paragraph>().ToArray();
            var first = paragraphs[0].Inlines.OfType<Run>().Single();
            var second = paragraphs[1].Inlines.OfType<Run>().Single();
            var start = first.ContentStart.GetPositionAtOffset(6)!;
            editor.Selection.Select(start, second.ContentStart.GetPositionAtOffset(6)!);
            Pump(window);
            var originalDocument = editor.Document;

            typeof(NoteWindow).GetMethod("ApplyStyle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [NoteTextStyle.Bold, true]);

            Assert.AreSame(originalDocument, editor.Document);
            var formatted = app.GetNote(noteId).Document;
            Assert.AreEqual("first line\r\nsecond line", formatted.GetPlainText());
            Assert.AreEqual(NoteTextStyle.None, formatted.Paragraphs[0].Runs.Single(run => run.Text == "first ").Style);
            Assert.AreEqual(NoteTextStyle.Bold, formatted.Paragraphs[0].Runs.Single(run => run.Text == "line").Style);
            Assert.AreEqual(NoteTextStyle.Bold, formatted.Paragraphs[1].Runs.Single(run => run.Text == "second").Style);
            Assert.AreEqual(NoteTextStyle.None, formatted.Paragraphs[1].Runs.Single(run => run.Text == " line").Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteTextStyle.Underline)]
    [DataRow(NoteTextStyle.Strikethrough)]
    public void MixedDecorationFormattingPreservesOtherDecorationsPerRun(NoteTextStyle style)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var firstStyle = NoteTextStyle.Bold | NoteTextStyle.Underline;
        var secondStyle = NoteTextStyle.Italic | NoteTextStyle.Strikethrough;
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Runs = [new NoteTextRun("first", firstStyle), new NoteTextRun(" second", secondStyle)]
            }]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var runs = editor.Document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>().ToArray();
            editor.Selection.Select(runs[0].ContentStart, runs[1].ContentEnd);
            Pump(window);
            var originalDocument = editor.Document;
            var buttonName = style == NoteTextStyle.Underline ? "UnderlineButton" : "StrikethroughButton";
            var button = (System.Windows.Controls.Primitives.ToggleButton)window.FindName(buttonName);
            Assert.IsNull(button.IsChecked, "Mixed existing decoration values remain visibly mixed before formatting.");
            typeof(NoteWindow).GetMethod("ApplyStyle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [style, true]);

            Assert.AreSame(originalDocument, editor.Document);
            var formatted = app.GetNote(noteId).Document;
            Assert.AreEqual(firstStyle | style, formatted.Paragraphs[0].Runs.Single(run => run.Text == "first").Style);
            Assert.AreEqual(secondStyle | style, formatted.Paragraphs[0].Runs.Single(run => run.Text == " second").Style);
            Assert.AreEqual("first second", formatted.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void PastedEmphasisConvertsAllFourMarkerStyles()
    {
        const string source = "**bold** *italic* __underlined__ ~~struck~~";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            PastePlainText(editor, source);
            Pump(window);

            var document = app.GetNote(noteId).Document;
            Assert.AreEqual("bold italic underlined struck", document.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold, document.Paragraphs[0].Runs.Single(run => run.Text == "bold").Style);
            Assert.AreEqual(NoteTextStyle.Italic, document.Paragraphs[0].Runs.Single(run => run.Text == "italic").Style);
            Assert.AreEqual(NoteTextStyle.Underline, document.Paragraphs[0].Runs.Single(run => run.Text == "underlined").Style);
            var struckRun = document.Paragraphs[0].Runs.FirstOrDefault(run => run.Text == "struck");
            Assert.IsTrue(struckRun is not null,
                string.Join(" | ", document.Paragraphs[0].Runs.Select(run => run.Style.ToString() + ':' + run.Kind.ToString() + ':' + run.Text)));
            Assert.AreEqual(NoteTextStyle.Strikethrough, struckRun!.Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("**", NoteTextStyle.Bold)]
    [DataRow("*", NoteTextStyle.Italic)]
    [DataRow("__", NoteTextStyle.Underline)]
    [DataRow("~~", NoteTextStyle.Strikethrough)]
    public void CharacterByCharacterEmphasisEndsAtClosingMarker(string marker, NoteTextStyle style)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            foreach (var character in marker + "styled" + marker)
            {
                Type(editor, character.ToString());
                Pump(window);
            }
            Type(editor, " plain");
            Pump(window);
            PressEnter(editor, window);
            Type(editor, "body");
            Pump(window);

            var document = app.GetNote(noteId).Document;
            Assert.AreEqual("styled plain\r\nbody", document.GetPlainText());
            Assert.AreEqual(style, document.Paragraphs[0].Runs.Single(run => run.Text == "styled").Style);
            Assert.AreEqual(NoteTextStyle.None, document.Paragraphs[0].Runs.Single(run => run.Text == " plain").Style);
            Assert.IsTrue(document.Paragraphs[1].Runs.All(run => run.Style == NoteTextStyle.None));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("BoldButton", NoteTextStyle.Bold)]
    [DataRow("ItalicButton", NoteTextStyle.Italic)]
    [DataRow("UnderlineButton", NoteTextStyle.Underline)]
    [DataRow("StrikethroughButton", NoteTextStyle.Strikethrough)]
    public void ToolbarCanDeactivateAnInheritedStyleAtTheCaret(string buttonName, NoteTextStyle style)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Runs = [new NoteTextRun("styled", style)] }]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = editor.Document.Blocks.OfType<Paragraph>().Single().ContentEnd;
            Pump(window);
            var button = (System.Windows.Controls.Primitives.ToggleButton)window.FindName(buttonName);
            Assert.AreEqual(true, button.IsChecked);
            button.IsChecked = false;
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            Type(editor, " plain");
            Pump(window);

            var document = app.GetNote(noteId).Document;
            Assert.AreEqual("styled plain", document.GetPlainText());
            Assert.AreEqual(style, document.Paragraphs[0].Runs.Single(run => run.Text == "styled").Style);
            Assert.AreEqual(NoteTextStyle.None, document.Paragraphs[0].Runs.Single(run => run.Text == " plain").Style);
            Assert.AreEqual(false, button.IsChecked);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }
    [TestMethod]
    public void ToolbarTypingStylesCanBeEnabledAndDisabledIndependently()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var buttons = new[] { "BoldButton", "ItalicButton", "UnderlineButton", "StrikethroughButton" }
                .Select(name => (System.Windows.Controls.Primitives.ToggleButton)window.FindName(name)).ToArray();
            foreach (var button in buttons)
            {
                button.IsChecked = true;
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
                Assert.AreEqual(true, button.IsChecked);
            }
            Assert.AreEqual(string.Empty, app.GetNote(noteId).Document.GetPlainText());
            Type(editor, "all");
            Pump(window);
            var remaining = NoteTextStyle.Bold | NoteTextStyle.Italic | NoteTextStyle.Underline | NoteTextStyle.Strikethrough;
            Assert.AreEqual(remaining, app.GetNote(noteId).Document.Paragraphs[0].Runs.Single().Style);
            var styles = new[] { NoteTextStyle.Bold, NoteTextStyle.Italic, NoteTextStyle.Underline, NoteTextStyle.Strikethrough };
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                button.IsChecked = false;
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
                Assert.AreEqual(false, button.IsChecked);
                remaining &= ~styles[index];
                Type(editor, " " + index);
                Pump(window);
                Assert.AreEqual(remaining, app.GetNote(noteId).Document.Paragraphs[0].Runs[^1].Style);
            }
            PressEnter(editor, window);
            Type(editor, "body");
            Pump(window);
            Assert.AreEqual(NoteTextStyle.None, app.GetNote(noteId).Document.Paragraphs[1].Runs.Single().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("BoldButton")]
    [DataRow("ItalicButton")]
    [DataRow("UnderlineButton")]
    [DataRow("StrikethroughButton")]
    [DataRow("BulletsButton")]
    [DataRow("NumberedButton")]
    [DataRow("ChecklistButton")]
    public void ToolbarFormattingPreservesScrolledViewport(string buttonName)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, NotesDocument.FromText(string.Join("\r\n", Enumerable.Range(0, 18).Select(index => $"Line {index:00}"))));
        var window = CreateWindow(app, id);
        try
        {
            window.Height = 240;
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var run = editor.Document.Blocks.OfType<Paragraph>().ElementAt(12).Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart, run.ContentEnd);
            Pump(window);
            var scroll = (ScrollViewer)editor.Template.FindName("PART_ContentHost", editor);
            scroll.ScrollToVerticalOffset(240);
            Pump(window);
            var offset = scroll.VerticalOffset;
            var selection = SelectionOffsets(window, app.GetNote(id).Document);
            Assert.IsTrue(offset > 0);
            Click((System.Windows.Controls.Primitives.ToggleButton)window.FindName(buttonName));
            Pump(window);
            Assert.AreEqual(offset, scroll.VerticalOffset, 0.5, $"Formatting changed scroll from {offset} to {scroll.VerticalOffset}.");
            Assert.AreEqual(selection, SelectionOffsets(window, app.GetNote(id).Document));
            Click((System.Windows.Controls.Primitives.ToggleButton)window.FindName(buttonName));
            Pump(window);
            Assert.AreEqual(offset, scroll.VerticalOffset, 0.5);
            Assert.AreEqual(selection, SelectionOffsets(window, app.GetNote(id).Document));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void StructuralFormattingFallbackPreservesLinksAndInlineMath()
    {
        const string destination = "https://example.com/notes";
        const string formula = "$x^2$";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Runs =
                [
                    new NoteTextRun("Guide", NoteTextStyle.None, NoteInlineKind.Link, destination),
                    new NoteTextRun(" and "),
                    new NoteTextRun(formula, NoteTextStyle.None, NoteInlineKind.InlineMath)
                ]
            }]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.SelectAll();
            var originalDocument = editor.Document;

            typeof(NoteWindow).GetMethod("ApplyStyle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [NoteTextStyle.Italic, true]);

            Assert.AreNotSame(originalDocument, editor.Document,
                "Selections containing semantic inline objects retain the conservative structural path.");
            var formatted = app.GetNote(noteId).Document;
            Assert.AreEqual("Guide and $x^2$", formatted.GetPlainText());
            var link = formatted.Paragraphs[0].Runs.Single(run => run.Kind == NoteInlineKind.Link);
            Assert.AreEqual("Guide", link.Text);
            Assert.AreEqual(destination, link.LinkDestination);
            Assert.AreEqual(NoteTextStyle.Italic, link.Style);
            var math = formatted.Paragraphs[0].Runs.Single(run => run.Kind == NoteInlineKind.InlineMath);
            Assert.AreEqual(formula, math.Text);
            Assert.AreEqual(NoteTextStyle.Italic, math.Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void FormattingInsideChecklistKeepsItsKindAndCheckedState()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Kind = NoteBlockKind.Checklist,
                IsChecked = true,
                Runs = [new NoteTextRun("buy milk")]
            }]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var run = editor.Document.Blocks.OfType<System.Windows.Documents.List>().Single()
                .ListItems.Cast<ListItem>().Single().Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart, run.ContentEnd);
            var originalDocument = editor.Document;

            typeof(NoteWindow).GetMethod("ApplyStyle", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [NoteTextStyle.Bold, true]);

            Assert.AreNotSame(originalDocument, editor.Document);
            var checklist = app.GetNote(noteId).Document.Paragraphs.Single();
            Assert.AreEqual(NoteBlockKind.Checklist, checklist.Kind);
            Assert.IsTrue(checklist.IsChecked);
            Assert.AreEqual(NoteTextStyle.Bold, checklist.Runs.Single().Style);
            Assert.AreEqual("buy milk", checklist.Runs.Single().Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ToolbarSelectionOffsetsSkipSyntheticContentAndReuseTheEditorMap()
    {
        var tableId = Guid.NewGuid();
        var source = new NotesDocument
        {
            Paragraphs =
            [
                new NoteParagraph { Runs = [new NoteTextRun("A\t😀\nZ")] },
                new NoteParagraph { Kind = NoteBlockKind.Numbered, Runs = [new NoteTextRun("One")] },
                new NoteParagraph { Kind = NoteBlockKind.Checklist, Runs = [new NoteTextRun("Task")] },
                new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("literal $$")] },
                new NoteParagraph { Cell = new NoteTableCell(tableId, 0, 0, NoteCellAlignment.Left), Runs = [new NoteTextRun("cell")] },
                new NoteParagraph { Cell = new NoteTableCell(tableId, 0, 1, NoteCellAlignment.Left),
                    Runs = [new NoteTextRun("$x$", NoteTextStyle.None, NoteInlineKind.InlineMath)] }
            ]
        };
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, source);
        var updateCalls = 0;
        var window = new NoteWindow(app.GetNote(noteId),
            (_, document) => { updateCalls++; app.UpdateDocument(noteId, document); return null; },
            (_, _) => null, (_, _) => null,
            (_, undo) =>
            {
                var moved = undo ? app.Undo(noteId) : app.Redo(noteId);
                return (moved ? app.GetNote(noteId).Document : null, null);
            },
            _ => (true, null), () => { }, () => { }, () => { },
            (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var root = editor.Document.Blocks;
            var first = root.OfType<Paragraph>().First();
            Assert.AreEqual((7, 0), SelectionAt(first.ContentEnd, 7));
            var map = typeof(NoteWindow).GetField("_editorOffsetMap", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window);

            var numbered = root.OfType<System.Windows.Documents.List>()
                .Single(list => Equals(list.Tag, NoteBlockKind.Numbered))
                .ListItems.Cast<ListItem>().Single().Blocks.OfType<Paragraph>().Single();
            var numberedRun = numbered.Inlines.OfType<Run>().Single();
            Assert.AreEqual((11, 0), SelectionAt(numberedRun.ContentStart.GetPositionAtOffset(2)!, 11));
            Assert.AreSame(map, typeof(NoteWindow).GetField("_editorOffsetMap", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window), "Caret movement should reuse the offset map while the document is unchanged.");
            Assert.AreEqual(true, ((ToggleButton)window.FindName("NumberedButton")).IsChecked);

            var checklist = root.OfType<System.Windows.Documents.List>()
                .Single(list => Equals(list.Tag, NoteBlockKind.Checklist))
                .ListItems.Cast<ListItem>().Single().Blocks.OfType<Paragraph>().Single();
            var checklistRun = checklist.Inlines.OfType<Run>().Single();
            editor.Selection.Select(numberedRun.ContentStart, checklistRun.ContentEnd);
            Pump(window);
            Assert.IsNull(((ToggleButton)window.FindName("NumberedButton")).IsChecked,
                "A mixed numbered/checklist selection must show a mixed list state.");
            Assert.IsNull(((ToggleButton)window.FindName("ChecklistButton")).IsChecked);

            var code = root.OfType<Paragraph>().Single(paragraph => Equals(paragraph.Tag, NoteBlockKind.Code));
            var codeRun = code.Inlines.OfType<Run>().Single();
            Assert.AreEqual((27, 0), SelectionAt(codeRun.ContentStart.GetPositionAtOffset(7)!, 27));
            var table = root.OfType<Table>().Single();
            var cells = table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).ToArray();
            var cellRun = cells[0].Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>().Single();
            Assert.AreEqual((36, 0), SelectionAt(cellRun.ContentEnd, 36));
            var math = cells[1].Blocks.OfType<Paragraph>().Single().Inlines.OfType<InlineUIContainer>().Single();
            Assert.AreEqual((41, 0), SelectionAt(math.ContentEnd, 41));
            Assert.AreEqual(0, updateCalls, "Toolbar observation must not save or edit the note.");
            Assert.AreEqual(source.GetPlainText(), app.GetNote(noteId).Document.GetPlainText());

            editor.CaretPosition = codeRun.ContentEnd;
            Type(editor, "X");
            Pump(window);
            Assert.AreEqual((31, 0), SelectionOffsets(window, app.GetNote(noteId).Document));
            Assert.AreNotSame(map, typeof(NoteWindow).GetField("_editorOffsetMap", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window), "A text edit must invalidate offsets tied to the old editor content.");
            Assert.AreEqual(1, updateCalls);

            (int Start, int Length) SelectionAt(TextPointer position, int expected)
            {
                editor.CaretPosition = position;
                Pump(window);
                var offsets = SelectionOffsets(window, app.GetNote(noteId).Document);
                Assert.AreEqual((expected, 0), offsets);
                return offsets;
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static (int Start, int Length) SelectionOffsets(NoteWindow window, NotesDocument document) =>
        ((int Start, int Length))typeof(NoteWindow).GetMethod("GetSelectionOffsets", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [document])!;

    private static void Click(System.Windows.Controls.Primitives.ToggleButton button) =>
        typeof(System.Windows.Controls.Primitives.ToggleButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(button, null);

    private static NoteWindow CreateWindow(NotesApplication app, Guid noteId) => new(
        app.GetNote(noteId),
        (_, document) => { app.UpdateDocument(noteId, document); return null; },
        (_, _) => null,
        (_, _) => null,
        (_, undo) =>
        {
            var moved = undo ? app.Undo(noteId) : app.Redo(noteId);
            return (moved ? app.GetNote(noteId).Document : null, null);
        },
        _ => (true, null),
        () => { },
        () => { },
        () => { },
        (_, _) => null,
        _ => null,
        () => null,
        new NotesConfiguration());

    private static void RestoreHistory(NoteWindow window, bool undo) =>
        typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [undo]);

    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.Left = window.Top = -10000;
        window.Show();
        window.Activate();
        Pump(window);
    }

    private static void Pump(Window window) =>
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private static void Type(RichTextBox editor, string text) =>
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, text));

    private static void PressEnter(RichTextBox editor, Window window)
    {
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(key);
        if (!key.Handled) EditingCommands.EnterParagraphBreak.Execute(null, editor);
        Pump(window);
    }
    private sealed class MemoryNotesStore : INotesStore
    {
        public NotesSnapshot? Load() => null;
        public void Save(NotesSnapshot snapshot) { }
    }
}
