using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;
using static DesktopPet.Windows.Tests.NoteClipboardTestInput;
using WpfMath.Controls;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteDisplayMathInteractionTests
{
    [TestMethod]
    [DataRow("$$", NoteBlockKind.DisplayMath)]
    [DataRow("```", NoteBlockKind.Code)]
    public void OpeningBlockShortcutSupportsUndoRedoAndReopening(string marker, NoteBlockKind kind)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        NoteWindow? reopened = null;
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            foreach (var character in marker)
            {
                Type(editor, character.ToString());
                Pump(window);
            }
            Assert.AreEqual(kind, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            RestoreHistory(window, undo: true);
            Pump(window);
            Assert.AreEqual(marker, app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            RestoreHistory(window, undo: false);
            Pump(window);
            Assert.AreEqual(kind, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            Assert.AreEqual(string.Empty, Text(app.GetNote(noteId).Document.Paragraphs[0]));
            reopened = CreateWindow(app, noteId);
            ShowHidden(reopened);
            var reopenedEditor = (RichTextBox)reopened.FindName("Editor");
            Assert.AreEqual(kind, ((Paragraph)reopenedEditor.Document.Blocks.FirstBlock).Tag);
        }
        finally
        {
            reopened?.CloseWithoutMembershipChange();
            window.CloseWithoutMembershipChange();
        }
    }

    [TestMethod]
    [DataRow("$$", false)]
    [DataRow("$$", true)]
    [DataRow("```", false)]
    [DataRow("```", true)]
    public void PastedAndRestoredOpeningMarkersStayLiteral(string marker, bool restored)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        if (restored) app.UpdateDocument(noteId, NotesDocument.FromText(marker));
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            if (!restored) PastePlainText(editor, marker);
            Pump(window);
            Assert.AreEqual(marker, app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs.Single().Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("before ")]
    public void OpeningDoubleDollarImmediatelyCreatesFocusedEditableMath(string prefix)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            if (prefix.Length > 0) Type(editor, prefix);
            Type(editor, "$");
            Pump(window);
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath));
            Type(editor, "$");
            Pump(window);

            var block = app.GetNote(noteId).Document.Paragraphs.Single(row => row.Kind == NoteBlockKind.DisplayMath);
            Assert.AreEqual(string.Empty, Text(block));
            Assert.AreEqual(1, DisplayMathSourceSpans(editor).Count());
            Type(editor, "x^2");
            Pump(window);
            Assert.AreEqual("x^2", Text(app.GetNote(noteId).Document.Paragraphs.Single(row => row.Kind == NoteBlockKind.DisplayMath)));
            Assert.AreEqual(1, DisplayMathSourceSpans(editor).Count());
            PressEnter(editor, window);
            Type(editor, "+y");
            Pump(window);
            Assert.AreEqual("x^2\r\n+y", NoteTextOffsets.NormalizeLineBreaks(Text(app.GetNote(noteId).Document.Paragraphs.Single(row => row.Kind == NoteBlockKind.DisplayMath))));
            if (prefix.Length > 0) Assert.AreEqual(prefix, Text(app.GetNote(noteId).Document.Paragraphs[0]));
            Assert.IsTrue((bool)typeof(NoteWindow).GetMethod("HandleEditorKey", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [Key.Enter, true, false])!);
            Type(editor, "body");
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs[prefix.Length > 0 ? 2 : 1].Kind);
            Assert.AreEqual("body", Text(app.GetNote(noteId).Document.Paragraphs[prefix.Length > 0 ? 2 : 1]));
            Assert.IsFalse(FindFormulaControl(DisplayMathButtons(editor).Single())!.HasError);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void PastedSameLineAndMultilineDisplayMathUseTheActualRenderer()
    {
        const string text = "before $$\\frac{1}{2}$$ after\r\n" +
                            "$$\r\n\\begin{pmatrix}a&b\\\\c&d\\end{pmatrix}\r\n$$\r\n" +
                            "$$\r\n\\begin{align}a&=b+c\\\\d&=e+f\\end{align}\r\n$$";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, text);
            Pump(window);

            var document = app.GetNote(noteId).Document;
            var displayBlocks = document.Paragraphs.Where(row => row.Kind == NoteBlockKind.DisplayMath).ToArray();
            Assert.AreEqual(3, displayBlocks.Length);
            Assert.AreEqual("$$\\frac{1}{2}$$", Text(displayBlocks[0]));
            Assert.AreEqual("$$\r\n\\begin{pmatrix}a&b\\\\c&d\\end{pmatrix}\r\n$$", Text(displayBlocks[1]));
            Assert.AreEqual("$$\r\n\\begin{align}a&=b+c\\\\d&=e+f\\end{align}\r\n$$", Text(displayBlocks[2]));

            var buttons = DisplayMathButtons(editor).ToArray();
            Assert.AreEqual(3, buttons.Length);
            foreach (var button in buttons)
            {
                Assert.AreEqual("Edit display equation source", AutomationProperties.GetName(button));
                var formula = FindFormulaControl(button);
                Assert.IsNotNull(formula);
                formula!.UpdateLayout();
                Assert.IsFalse(formula.HasError, "The actual local renderer must accept each display formula.");
                Assert.IsTrue(formula.ActualWidth > 0 && formula.ActualHeight > 0);
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void TypingClosingDoubleDollarCreatesABlockAndKeepsNeighborText()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, "left $$x^2$");
            Pump(window);
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath));

            Type(editor, "$");
            Pump(window);
            Assert.AreEqual(NoteBlockKind.DisplayMath, app.GetNote(noteId).Document.Paragraphs[1].Kind);
            Assert.AreEqual("$$x^2$$", Text(app.GetNote(noteId).Document.Paragraphs[1]));
            Type(editor, " after");
            Pump(window);
            var completed = app.GetNote(noteId).Document;
            Assert.AreEqual("left \r\n$$x^2$$\r\n after", completed.GetPlainText(),
                string.Join(" | ", completed.Paragraphs.Select(row => $"{row.Kind}:{Text(row)}")));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void IncompleteDisplaySourceStaysLiteralUntilItsClosingDelimiterIsAdded()
    {
        const string incomplete = "before\r\n$$\r\n# literal heading\r\n*literal emphasis*";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, incomplete);
            Pump(window);
            var saved = app.GetNote(noteId).Document;
            Assert.AreEqual(incomplete, saved.GetPlainText());
            Assert.IsFalse(saved.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath));
            Assert.IsFalse(saved.Paragraphs.Any(row => row.Kind is NoteBlockKind.Heading1 or NoteBlockKind.Heading2));
            Assert.IsFalse(saved.Paragraphs.SelectMany(row => row.Runs).Any(run => run.Style.HasFlag(NoteTextStyle.Italic)));

            PastePlainText(editor, "\r\n$$");
            Pump(window);
            var completed = app.GetNote(noteId).Document;
            Assert.IsTrue(completed.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath),
                $"Closing delimiter was not recognized: {completed.GetPlainText()} | " +
                string.Join(" | ", completed.Paragraphs.Select(row => $"{row.Kind}:{Text(row)}")) +
                $" | status:{((TextBlock)window.FindName("SaveStatus")).Text}");
            var block = completed.Paragraphs.Single(row => row.Kind == NoteBlockKind.DisplayMath);
            Assert.AreEqual("$$\r\n# literal heading\r\n*literal emphasis*\r\n$$", Text(block));
            Assert.AreEqual(1, DisplayMathButtons(editor).Count());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void InvalidDisplaySourceStaysEditableAndRendersAfterCorrection()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, "$$\\unknowncommand$$");
            Pump(window);
            var saved = app.GetNote(noteId).Document;
            Assert.IsTrue(saved.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath),
                $"Invalid source was not made semantic: {saved.GetPlainText()} | " +
                string.Join(" | ", saved.Paragraphs.Select(row => $"{row.Kind}:{Text(row)}")) +
                $" | status:{((TextBlock)window.FindName("SaveStatus")).Text}");
            var invalid = DisplayMathButtons(editor).Single();
            Assert.IsTrue(invalid.Content is StackPanel);
            Assert.AreEqual("This equation could not be rendered. Activate it to edit the source.", invalid.ToolTip);
            Assert.IsTrue(editor.Focus());
            invalid.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(window);

            Assert.AreEqual(1, DisplayMathSourceSpans(editor).Count(), "Clicking invalid display math should reveal its source.");
            var sourceRun = DisplayMathSourceSpans(editor).Single().Inlines.OfType<Run>().Single();
            editor.Selection.Select(sourceRun.ContentStart.GetPositionAtOffset(2)!,
                sourceRun.ContentStart.GetPositionAtOffset(sourceRun.Text.Length - 2)!);
            PastePlainText(editor, "\\frac{1}{2}");
            var trailing = editor.Document.Blocks.OfType<Paragraph>().Last();
            editor.CaretPosition = trailing.ContentEnd;
            Pump(window);

            var rendered = DisplayMathButtons(editor).Single();
            Assert.IsFalse(rendered.Content is StackPanel);
            Assert.IsFalse(FindFormulaControl(rendered)!.HasError);
            Assert.AreEqual("$$\\frac{1}{2}$$", Text(app.GetNote(noteId).Document.Paragraphs[0]));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ContextMenuWrapsOnlySelectedTextAndEmptyBlockStartsFocusedSourceEditing()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, "before wrap me after");
            Pump(window);
            var sourceRun = editor.Document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>().Single();
            editor.Selection.Select(sourceRun.ContentStart.GetPositionAtOffset(7)!,
                sourceRun.ContentStart.GetPositionAtOffset(14)!);
            InvokeCreateMathBlock(window);

            var wrapped = app.GetNote(noteId).Document;
            Assert.AreEqual("before ", Text(wrapped.Paragraphs[0]));
            Assert.AreEqual(NoteBlockKind.DisplayMath, wrapped.Paragraphs[1].Kind);
            Assert.AreEqual("wrap me", Text(wrapped.Paragraphs[1]));
            Assert.AreEqual(" after", Text(wrapped.Paragraphs[2]));
            Assert.IsTrue(editor.IsKeyboardFocused);
            Assert.AreEqual("wrap me", DisplayMathSourceSpans(editor).Single().Inlines.OfType<Run>().Single().Text);

            var emptyApp = NotesApplication.Start(new MemoryNotesStore());
            var emptyId = emptyApp.CreateNote();
            var emptyWindow = CreateWindow(emptyApp, emptyId);
            try
            {
                ShowHidden(emptyWindow);
                var emptyEditor = (RichTextBox)emptyWindow.FindName("Editor");
                InvokeCreateMathBlock(emptyWindow);
                Assert.AreEqual(NoteBlockKind.DisplayMath, emptyApp.GetNote(emptyId).Document.Paragraphs[0].Kind);
                Assert.IsTrue(emptyEditor.IsKeyboardFocused);
                Assert.AreEqual(1, DisplayMathSourceSpans(emptyEditor).Count());
                Type(emptyEditor, "\\frac{1}{2}");
                Pump(emptyWindow);
                Assert.AreEqual("\\frac{1}{2}", Text(emptyApp.GetNote(emptyId).Document.Paragraphs[0]));
                PressEnter(emptyEditor, emptyWindow);
                Assert.AreEqual("\\frac{1}{2}\r\n", NoteTextOffsets.NormalizeLineBreaks(
                    Text(emptyApp.GetNote(emptyId).Document.Paragraphs[0])));
                Type(emptyEditor, "+x");
                Pump(emptyWindow);

                var source = emptyApp.GetNote(emptyId).Document.Paragraphs[0];
                Assert.AreEqual(NoteBlockKind.DisplayMath, source.Kind);
                Assert.AreEqual("\\frac{1}{2}\r\n+x", NoteTextOffsets.NormalizeLineBreaks(Text(source)));

                var trailing = (Paragraph)emptyEditor.Document.Blocks.LastBlock;
                emptyEditor.CaretPosition = trailing.ContentStart;
                Pump(emptyWindow);
                Assert.AreEqual(1, DisplayMathButtons(emptyEditor).Count());
                Assert.AreEqual("\\frac{1}{2}\r\n+x", NoteTextOffsets.NormalizeLineBreaks(
                    Text(emptyApp.GetNote(emptyId).Document.Paragraphs[0])));
            }
            finally { emptyWindow.CloseWithoutMembershipChange(); }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void DisplayBlockCopyAddsDelimitersAndRichPasteKeepsItsSemanticType()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        var targetApp = NotesApplication.Start(new MemoryNotesStore());
        var targetId = targetApp.CreateNote();
        var target = CreateWindow(targetApp, targetId);
        try
        {
            ShowHidden(window);
            ShowHidden(target);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, "before selected after");
            Pump(window);
            var run = editor.Document.Blocks.OfType<Paragraph>().Single().Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart.GetPositionAtOffset(7)!, run.ContentStart.GetPositionAtOffset(15)!);
            InvokeCreateMathBlock(window);
            Assert.AreEqual("selected", Text(app.GetNote(noteId).Document.Paragraphs[1]));
            editor.CaretPosition = editor.Document.Blocks.OfType<Paragraph>().Last().ContentEnd;
            Pump(window);

            var display = DisplayMathContainers(editor).Single();
            editor.Selection.Select(display.ContentStart, display.ContentEnd);
            var data = new DataObject(DataFormats.UnicodeText, string.Empty);
            editor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            Assert.AreEqual("$$selected$$", data.GetData(DataFormats.UnicodeText)!);
            Assert.IsTrue(data.GetDataPresent("Hood.Notes.Semantic.v1"));

            editor.Selection.Select(editor.Document.ContentStart, editor.Document.ContentEnd);
            data = new DataObject(DataFormats.UnicodeText, string.Empty);
            editor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            Assert.AreEqual("before \r\n$$selected$$\r\n after", data.GetData(DataFormats.UnicodeText)!);
            Assert.IsTrue(data.GetDataPresent("Hood.Notes.Semantic.v1"));

            var targetEditor = (RichTextBox)target.FindName("Editor");
            var paste = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText);
            targetEditor.RaiseEvent(paste);
            Assert.IsTrue(paste.CommandCancelled);
            Pump(target);
            Assert.AreEqual("before \r\nselected\r\n after", targetApp.GetNote(targetId).Document.GetPlainText());
            Assert.AreEqual(NoteBlockKind.DisplayMath, targetApp.GetNote(targetId).Document.Paragraphs[1].Kind);
            Assert.AreEqual("selected", Text(targetApp.GetNote(targetId).Document.Paragraphs[1]));
            Assert.AreEqual(1, DisplayMathButtons(targetEditor).Count());
        }
        finally
        {
            window.CloseWithoutMembershipChange();
            target.CloseWithoutMembershipChange();
        }
    }

    [TestMethod]
    public void DisplayBlockDeletionAndUndoKeepNeighboringParagraphs()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, "before $$x$$ after");
            Pump(window);
            var container = DisplayMathContainers(editor).Single();
            editor.Selection.Select(container.ContentStart, container.ContentEnd);
            Assert.IsTrue(editor.Focus());
            var selection = ((int Start, int Length))typeof(NoteWindow)
                .GetMethod("GetSelectionOffsets", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [app.GetNote(noteId).Document])!;
            Assert.AreEqual(9, selection.Start, $"Unexpected display source start: {selection.Start}, {selection.Length}.");
            Assert.AreEqual(5, selection.Length, $"Unexpected selected display source range: {selection.Start}, {selection.Length}.");
            Assert.IsTrue(PressBackspace(editor, window), "Backspace should remove the selected rendered display block.");

            var removed = app.GetNote(noteId).Document;
            Assert.AreEqual("before \r\n after", removed.GetPlainText(),
                string.Join(" | ", removed.Paragraphs.Select(row => $"{row.Kind}:{Text(row)}")));
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.Any(row => row.Kind == NoteBlockKind.DisplayMath));
            RestoreHistory(window, undo: true);
            Pump(window);
            Assert.AreEqual(NoteBlockKind.DisplayMath, app.GetNote(noteId).Document.Paragraphs[1].Kind);
            RestoreHistory(window, undo: false);
            Pump(window);
            Assert.AreEqual("before \r\n after", app.GetNote(noteId).Document.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void LongDisplayOutputKeepsItsSizeInsideAScrollableViewport()
    {
        var expression = string.Join("+", Enumerable.Repeat("x", 120));
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, "$$" + expression + "$$");
            Pump(window);
            var button = DisplayMathButtons(editor).Single();
            Assert.IsInstanceOfType<ScrollViewer>(button.Content);
            var viewer = (ScrollViewer)button.Content;
            Assert.AreEqual(ScrollBarVisibility.Auto, viewer.HorizontalScrollBarVisibility);
            Assert.AreEqual(ScrollBarVisibility.Auto, viewer.VerticalScrollBarVisibility);
            Assert.IsTrue(double.IsFinite(viewer.MaxWidth) && viewer.MaxWidth > 1);
            Assert.IsTrue(double.IsFinite(viewer.MaxHeight) && viewer.MaxHeight > 1);
            Assert.IsTrue(FindFormulaControl(button)!.ActualWidth > 0);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CreateMathBlockFromCodeContextPlacesItBesideCodeAndDeclinesCodeSelection()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("code")] }]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            InvokeCreateMathBlock(window);
            var document = app.GetNote(noteId).Document;
            Assert.AreEqual(NoteBlockKind.Code, document.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.DisplayMath, document.Paragraphs[1].Kind);

            var editor = (RichTextBox)window.FindName("Editor");
            var codeRun = editor.Document.Blocks.OfType<Paragraph>().First().Inlines.OfType<Run>().Single();
            editor.Selection.Select(codeRun.ContentStart, codeRun.ContentEnd);
            InvokeCreateMathBlock(window);
            Assert.AreEqual(NoteBlockKind.Code, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.DisplayMath, app.GetNote(noteId).Document.Paragraphs[1].Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CreateMathBlockFromTableContextPlacesItAfterTheTableAndDeclinesTableSelection()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var tableId = Guid.NewGuid();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs =
            [
                new NoteParagraph { Cell = new NoteTableCell(tableId, 0, 0), Runs = [new NoteTextRun("cell text")] },
                new NoteParagraph { Runs = [new NoteTextRun("after")] }
            ]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            InvokeCreateMathBlock(window);
            var inserted = app.GetNote(noteId).Document;
            Assert.AreEqual(tableId, inserted.Paragraphs[0].Cell!.TableId);
            Assert.AreEqual(NoteBlockKind.DisplayMath, inserted.Paragraphs[1].Kind);
            Assert.AreEqual("after", Text(inserted.Paragraphs[2]));

            var editor = (RichTextBox)window.FindName("Editor");
            var table = editor.Document.Blocks.OfType<Table>().Single();
            var cellRun = table.RowGroups.SelectMany(group => group.Rows)
                .SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks.OfType<Paragraph>())
                .SelectMany(paragraph => paragraph.Inlines.OfType<Run>()).Single();
            editor.Selection.Select(cellRun.ContentStart, cellRun.ContentEnd);
            Assert.IsTrue(editor.Focus());
            InvokeCreateMathBlock(window);

            var afterRejectedSelection = app.GetNote(noteId).Document;
            Assert.AreEqual("cell text", Text(afterRejectedSelection.Paragraphs[0]));
            Assert.AreEqual(NoteBlockKind.DisplayMath, afterRejectedSelection.Paragraphs[1].Kind);
            Assert.AreEqual("after", Text(afterRejectedSelection.Paragraphs[2]));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EnterAfterPastedDisplayMathStartsOrdinaryText(bool richPaste)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        var sourceApp = NotesApplication.Start(new MemoryNotesStore());
        var sourceId = sourceApp.CreateNote();
        var sourceWindow = CreateWindow(sourceApp, sourceId);
        try
        {
            ShowHidden(sourceWindow);
            var sourceEditor = (RichTextBox)sourceWindow.FindName("Editor");
            var sourceData = new DataObject(DataFormats.UnicodeText, "$$x^2$$");
            sourceEditor.RaiseEvent(new DataObjectPastingEventArgs(sourceData, false, DataFormats.UnicodeText));
            sourceEditor.Selection.Text = "$$x^2$$";
            Pump(sourceWindow);
            var container = DisplayMathContainers(sourceEditor).Single();
            sourceEditor.Selection.Select(container.ContentStart, container.ContentEnd);
            var data = new DataObject(DataFormats.UnicodeText, string.Empty);
            sourceEditor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            Assert.AreEqual("$$x^2$$", data.GetData(DataFormats.UnicodeText)!);

            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            if (richPaste)
                editor.RaiseEvent(new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText));
            else
            {
                var plainData = new DataObject(DataFormats.UnicodeText, data.GetData(DataFormats.UnicodeText)!);
                var paste = new DataObjectPastingEventArgs(plainData, false, DataFormats.UnicodeText);
                editor.RaiseEvent(paste);
                Assert.IsFalse(paste.CommandCancelled);
                editor.Selection.Text = (string)plainData.GetData(DataFormats.UnicodeText)!;
                editor.CaretPosition = editor.Selection.End;
            }
            Pump(window);
            Assert.AreEqual(1, DisplayMathButtons(editor).Count());

            var paragraphCountBeforeEnter = app.GetNote(noteId).Document.Paragraphs.Count;
            PressEnter(editor, window);
            Type(editor, "body");
            Pump(window);
            var document = app.GetNote(noteId).Document;
            Assert.AreEqual(paragraphCountBeforeEnter + 1, document.Paragraphs.Count);
            Assert.AreEqual(NoteBlockKind.DisplayMath, document.Paragraphs[0].Kind);
            Assert.AreEqual("$$x^2$$", Text(document.Paragraphs[0]));
            Assert.AreEqual(NoteBlockKind.Paragraph, document.Paragraphs[^1].Kind);
            Assert.AreEqual("body", Text(document.Paragraphs[^1]));
            Assert.AreEqual(1, document.Paragraphs.Count(row => row.Kind == NoteBlockKind.DisplayMath));
            Assert.AreEqual(1, DisplayMathButtons(editor).Count());
        }
        finally
        {
            sourceWindow.CloseWithoutMembershipChange();
            window.CloseWithoutMembershipChange();
        }
    }
    [TestMethod]
    [DataRow(false, "x")]
    [DataRow(true, "x")]
    [DataRow(false, "")]
    [DataRow(true, "")]
    public void EnterBesideRenderedDisplayMathPreservesSourceAndHistory(bool before, string source)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.DisplayMath,
                Runs = source.Length == 0 ? [] : [new NoteTextRun(source)] }]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var container = DisplayMathContainers(editor).Single();
            editor.CaretPosition = before ? container.ContentStart : container.ContentEnd;
            PressEnter(editor, window);
            var split = app.GetNote(noteId).Document;
            Assert.AreEqual(2, split.Paragraphs.Count);
            Assert.AreEqual(NoteBlockKind.Paragraph, split.Paragraphs[before ? 0 : 1].Kind);
            Assert.AreEqual(NoteBlockKind.DisplayMath, split.Paragraphs[before ? 1 : 0].Kind);
            Assert.AreEqual(source, Text(split.Paragraphs[before ? 1 : 0]));
            RestoreHistory(window, undo: true);
            Pump(window);
            Assert.AreEqual(1, app.GetNote(noteId).Document.Paragraphs.Count);
            Assert.AreEqual(source, Text(app.GetNote(noteId).Document.Paragraphs[0]));
            RestoreHistory(window, undo: false);
            Pump(window);
            editor.CaretPosition = editor.Document.Blocks.OfType<Paragraph>().ElementAt(before ? 0 : 1).ContentStart;
            Type(editor, "body");
            Pump(window);
            var completed = app.GetNote(noteId).Document;
            Assert.AreEqual("body", Text(completed.Paragraphs[before ? 0 : 1]));
            Assert.AreEqual(NoteBlockKind.Paragraph, completed.Paragraphs[before ? 0 : 1].Kind);
            Assert.AreEqual(source, Text(completed.Paragraphs[before ? 1 : 0]));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

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

    private static IEnumerable<InlineUIContainer> DisplayMathContainers(RichTextBox editor) =>
        editor.Document.Blocks.OfType<Paragraph>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<InlineUIContainer>())
            .Where(container => container.Child is Button button &&
                                AutomationProperties.GetName(button) == "Edit display equation source");

    private static IEnumerable<Button> DisplayMathButtons(RichTextBox editor) =>
        DisplayMathContainers(editor).Select(container => (Button)container.Child);

    private static IEnumerable<Span> DisplayMathSourceSpans(RichTextBox editor) =>
        editor.Document.Blocks.OfType<Paragraph>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<Span>())
            .Where(span => span.GetType().Name == "NoteInlineMathSourceSpan" &&
                           span.GetType().GetProperty("IsDisplayMath")?.GetValue(span) as bool? == true);

    private static FormulaControl? FindFormulaControl(Button button) => button.Content switch
    {
        FormulaControl formula => formula,
        ScrollViewer viewer => viewer.Content as FormulaControl,
        _ => null
    };

    private static string Text(NoteParagraph paragraph) => string.Concat(paragraph.Runs.Select(run => run.Text));

    private static void InvokeCreateMathBlock(NoteWindow window)
    {
        var editor = (RichTextBox)window.FindName("Editor");
        var item = editor.ContextMenu!.Items.OfType<MenuItem>().Single(menuItem =>
            menuItem.Header?.ToString() == "Create math block");
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Pump(window);
    }

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

    private static bool PressBackspace(RichTextBox editor, Window window)
    {
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Back)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(key);
        if (!key.Handled) EditingCommands.Backspace.Execute(null, editor);
        Pump(window);
        return key.Handled;
    }

    private sealed class MemoryNotesStore : INotesStore
    {
        public NotesSnapshot? Load() => null;
        public void Save(NotesSnapshot snapshot) { }
    }
}
