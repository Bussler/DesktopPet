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
public sealed class NoteInlineMathInteractionTests
{
    [TestMethod]
    public void PastedCommonAndAdvancedFormulasRenderThroughTheLocalWpfMathControl()
    {
        const string text = "F $\\frac{1}{2}$ R $\\sqrt{x}$ S $x_1^2$ G $\\alpha+\\beta$ " +
                            "O $\\sum_{i=1}^{n} i$ I $\\int_0^1 x^2\\,dx$ " +
                            "M $\\begin{pmatrix}a&b\\\\c&d\\end{pmatrix}$ " +
                            "A $\\begin{align}a&=b+c\\\\d&=e+f\\end{align}$.";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            PastePlainText(editor, text);
            Pump(window);

            var saved = app.GetNote(noteId).Document;
            Assert.AreEqual(text, saved.GetPlainText());
            Assert.AreEqual(8, saved.Paragraphs.SelectMany(row => row.Runs)
                .Count(run => run.Kind == NoteInlineKind.InlineMath));

            var buttons = MathButtons(editor).ToArray();
            Assert.AreEqual(8, buttons.Length);
            foreach (var button in buttons)
            {
                Assert.AreEqual("Edit inline equation source", AutomationProperties.GetName(button));
                var formula = FindFormulaControl(button);
                Assert.IsNotNull(formula);
                formula!.UpdateLayout();
                Assert.IsFalse(formula.HasError, "A required formula failed to render through WpfMath.");
                Assert.IsTrue(formula.ActualWidth > 0 && formula.ActualHeight > 0,
                    "The actual WPF renderer must produce visible formula content.");
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void TypingClosingDollarConvertsAndLeavesTheCaretAfterTheRenderedEquation()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, "calc $x");
            Pump(window);
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.InlineMath));

            Type(editor, "$");
            Pump(window);
            Assert.AreEqual("calc $x$", app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual("$x$", app.GetNote(noteId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.InlineMath).Text);
            Assert.AreEqual(1, MathButtons(editor).Count());

            Type(editor, " after");
            Pump(window);
            Assert.AreEqual("calc $x$ after", app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(1, app.GetNote(noteId).Document.Paragraphs[0].Runs
                .Count(run => run.Kind == NoteInlineKind.InlineMath));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ExistingEquationRefreshesWithTheCurrentNoteAppearanceAndTextSize()
    {
        const string text = "value $\\alpha$";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, text);
            Pump(window);
            var formula = FindFormulaControl(MathButtons(editor).Single())!;

            window.ApplyAppearance(new NotesConfiguration { Theme = "dark", TextSize = 24 }, NoteColor.Pink);
            Pump(window);

            Assert.AreEqual(24, formula.Scale);
            Assert.AreEqual(editor.Foreground, formula.Foreground);
            Assert.AreEqual(text, app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(NoteInlineKind.InlineMath, app.GetNote(noteId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.InlineMath).Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ClickEditsInvalidSourceInPlaceAndCorrectionRendersAgain()
    {
        const string text = "before $x^2$ after";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, text);
            Pump(window);

            MathButtons(editor).Single().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(window);
            var sourceSpan = SourceSpans(editor).Single();
            var sourceRun = sourceSpan.Inlines.OfType<Run>().Single();
            Assert.AreEqual("$x^2$", sourceRun.Text);
            Assert.IsTrue(editor.IsKeyboardFocused);
            Assert.IsTrue(editor.Selection.Start.CompareTo(sourceSpan.ContentStart) >= 0);
            Assert.IsTrue(editor.Selection.Start.CompareTo(sourceSpan.ContentEnd) <= 0);

            var partialCopyStart = sourceRun.ContentStart.GetPositionAtOffset(1)!;
            var partialCopyEnd = sourceRun.ContentStart.GetPositionAtOffset(3)!;
            editor.Selection.Select(partialCopyStart, partialCopyEnd);
            var partialCopy = new DataObject(DataFormats.UnicodeText, string.Empty);
            editor.RaiseEvent(new DataObjectCopyingEventArgs(partialCopy, false));
            Assert.AreEqual("x^", partialCopy.GetData(DataFormats.UnicodeText));
            Assert.IsFalse(partialCopy.GetDataPresent("Hood.Notes.Semantic.v1"));

            var sourceStart = sourceRun.ContentStart.GetPositionAtOffset(1)!;
            var sourceEnd = sourceRun.ContentStart.GetPositionAtOffset(sourceRun.Text.Length - 1)!;
            editor.Selection.Select(sourceStart, sourceEnd);
            PastePlainText(editor, "\\unknowncommand");
            Pump(window);
            Assert.AreEqual("$\\unknowncommand$", app.GetNote(noteId).Document.Paragraphs[0]
                .Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);

            editor.CaretPosition = ((Paragraph)editor.Document.Blocks.FirstBlock).ContentEnd;
            Pump(window);
            var invalid = MathButtons(editor).Single();
            Assert.IsTrue(invalid.Content is StackPanel, "Unsupported formulas need a local error marker.");
            Assert.IsTrue(FindFormulaControl(invalid)!.HasError);
            Assert.AreEqual("$\\unknowncommand$", app.GetNote(noteId).Document.Paragraphs[0]
                .Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);

            invalid.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(window);
            sourceSpan = SourceSpans(editor).Single();
            sourceRun = sourceSpan.Inlines.OfType<Run>().Single();
            editor.Selection.Select(
                sourceRun.ContentStart.GetPositionAtOffset(1)!,
                sourceRun.ContentStart.GetPositionAtOffset(sourceRun.Text.Length - 1)!);
            PastePlainText(editor, "\\frac{1}{2}");
            editor.CaretPosition = ((Paragraph)editor.Document.Blocks.FirstBlock).ContentEnd;
            Pump(window);
            Assert.IsFalse(MathButtons(editor).Single().Content is StackPanel);
            Assert.IsFalse(FindFormulaControl(MathButtons(editor).Single())!.HasError);
            Assert.AreEqual("$\\frac{1}{2}$", app.GetNote(noteId).Document.Paragraphs[0]
                .Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CopyProjectsDelimitedSourceAndRichPasteRestoresSemanticMath()
    {
        const string text = "before $x^2$ after";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var source = CreateWindow(app, noteId);
        var targetApp = NotesApplication.Start(new MemoryNotesStore());
        var targetId = targetApp.CreateNote();
        var target = CreateWindow(targetApp, targetId);
        try
        {
            ShowHidden(source);
            ShowHidden(target);
            var editor = (RichTextBox)source.FindName("Editor");
            PastePlainText(editor, text);
            Pump(source);

            var formula = MathContainers(editor).Single();
            editor.Selection.Select(formula.ContentStart, formula.ContentEnd);
            var data = new DataObject(DataFormats.UnicodeText, string.Empty);
            editor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            Assert.AreEqual("$x^2$", data.GetData(DataFormats.UnicodeText));
            Assert.IsTrue(data.GetDataPresent("Hood.Notes.Semantic.v1"));

            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock;
            editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
            data = new DataObject(DataFormats.UnicodeText, string.Empty);
            editor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            Assert.AreEqual(text, data.GetData(DataFormats.UnicodeText));

            var targetEditor = (RichTextBox)target.FindName("Editor");
            targetEditor.SelectAll();
            var paste = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText);
            targetEditor.RaiseEvent(paste);
            Assert.IsTrue(paste.CommandCancelled, "Semantic math clipboard data should use the Notes paste path.");
            Pump(target);

            var pasted = targetApp.GetNote(targetId).Document;
            Assert.AreEqual(text, pasted.GetPlainText());
            Assert.AreEqual("$x^2$", pasted.Paragraphs[0].Runs.Single(run =>
                run.Kind == NoteInlineKind.InlineMath).Text);
            Assert.AreEqual(1, MathButtons(targetEditor).Count());
        }
        finally
        {
            source.CloseWithoutMembershipChange();
            target.CloseWithoutMembershipChange();
        }
    }

    [TestMethod]
    public void SelectingAndDeletingRenderedEquationPreservesNeighborsAndHistory()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, "before $x$ after");
            Pump(window);

            var formula = MathContainers(editor).Single();
            editor.Selection.Select(formula.ContentStart, formula.ContentEnd);
            PressBackspace(editor, window);
            Assert.AreEqual("before  after", app.GetNote(noteId).Document.GetPlainText());
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.InlineMath));

            RestoreHistory(window, undo: true);
            Pump(window);
            Assert.AreEqual("before $x$ after", app.GetNote(noteId).Document.GetPlainText());
            Assert.IsTrue(app.GetNote(noteId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.InlineMath));

            RestoreHistory(window, undo: false);
            Pump(window);
            Assert.AreEqual("before  after", app.GetNote(noteId).Document.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void UndoAndRedoRestoreLiteralThenSemanticMathWithoutReconversion()
    {
        const string text = "value $x^2$";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, text);
            Pump(window);
            Assert.AreEqual(NoteInlineKind.InlineMath, app.GetNote(noteId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.InlineMath).Kind);

            RestoreHistory(window, undo: true);
            Pump(window);
            Assert.AreEqual(text, app.GetNote(noteId).Document.GetPlainText());
            Assert.IsTrue(app.GetNote(noteId).Document.Paragraphs[0].Runs.All(run => run.Kind == NoteInlineKind.Text));

            RestoreHistory(window, undo: false);
            Pump(window);
            Assert.AreEqual(NoteInlineKind.InlineMath, app.GetNote(noteId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.InlineMath).Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void KeyboardNavigationOutOfTheSourceRebuildsTheRenderedEquation()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            PastePlainText(editor, "before $x$ after");
            Pump(window);
            MathButtons(editor).Single().RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(window);

            for (var index = 0; index < 5 && !MathButtons(editor).Any(); index++)
            {
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Right)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                editor.RaiseEvent(key);
                if (!key.Handled) EditingCommands.MoveRightByCharacter.Execute(null, editor);
                Pump(window);
            }

            Assert.AreEqual(1, MathButtons(editor).Count());
            Assert.AreEqual("before $x$ after", app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(NoteInlineKind.InlineMath, app.GetNote(noteId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.InlineMath).Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void MathClipboardPasteIntoCodePreservesLiteralSourceInOneBlock()
    {
        var sourceApp = NotesApplication.Start(new MemoryNotesStore());
        var sourceId = sourceApp.CreateNote();
        var source = CreateWindow(sourceApp, sourceId);
        var targetApp = NotesApplication.Start(new MemoryNotesStore());
        var targetId = targetApp.CreateNote();
        var codeDocument = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("code")] }]
        };
        targetApp.UpdateDocument(targetId, codeDocument);
        var target = CreateWindow(targetApp, targetId);
        try
        {
            ShowHidden(source);
            ShowHidden(target);
            var sourceEditor = (RichTextBox)source.FindName("Editor");
            PastePlainText(sourceEditor, "$x$");
            Pump(source);
            var formula = MathContainers(sourceEditor).Single();
            sourceEditor.Selection.Select(formula.ContentStart, formula.ContentEnd);
            var data = new DataObject(DataFormats.UnicodeText, string.Empty);
            sourceEditor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            Assert.IsTrue(data.GetDataPresent("Hood.Notes.Semantic.v1"));

            var targetEditor = (RichTextBox)target.FindName("Editor");
            targetEditor.CaretPosition = ((Paragraph)targetEditor.Document.Blocks.FirstBlock).ContentEnd;
            var paste = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText);
            targetEditor.RaiseEvent(paste);

            Assert.IsTrue(paste.CommandCancelled);
            Pump(target);
            var pasted = targetApp.GetNote(targetId).Document;
            Assert.AreEqual("code$x$", pasted.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, pasted.Paragraphs.Single().Kind);
            Assert.IsTrue(pasted.Paragraphs.Single().Runs.All(run => run.Kind == NoteInlineKind.Text));
            Assert.AreEqual(0, MathContainers(targetEditor).Count());
            RestoreHistory(target, undo: true);
            Pump(target);
            Assert.AreEqual("code", targetApp.GetNote(targetId).Document.GetPlainText());
            RestoreHistory(target, undo: false);
            Pump(target);
            Assert.AreEqual("code$x$", targetApp.GetNote(targetId).Document.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, targetApp.GetNote(targetId).Document.Paragraphs.Single().Kind);
        }
        finally
        {
            source.CloseWithoutMembershipChange();
            target.CloseWithoutMembershipChange();
        }
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

    private static IEnumerable<InlineUIContainer> MathContainers(RichTextBox editor) =>
        editor.Document.Blocks.OfType<Paragraph>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<InlineUIContainer>())
            .Where(container => container.Child is Button button &&
                                AutomationProperties.GetName(button) == "Edit inline equation source");

    private static IEnumerable<Button> MathButtons(RichTextBox editor) =>
        MathContainers(editor).Select(container => (Button)container.Child);

    private static IEnumerable<Span> SourceSpans(RichTextBox editor) =>
        editor.Document.Blocks.OfType<Paragraph>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<Span>())
            .Where(span => span.GetType().Name == "NoteInlineMathSourceSpan");

    private static FormulaControl? FindFormulaControl(Button button) =>
        button.Content as FormulaControl ??
        (button.Content as Panel)?.Children.OfType<FormulaControl>().SingleOrDefault();

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

    private static void PressBackspace(RichTextBox editor, Window window)
    {
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Back)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(key);
        if (!key.Handled) EditingCommands.Backspace.Execute(null, editor);
        Pump(window);
    }

    private sealed class MemoryNotesStore : INotesStore
    {
        public NotesSnapshot? Load() => null;
        public void Save(NotesSnapshot snapshot) { }
    }
}
