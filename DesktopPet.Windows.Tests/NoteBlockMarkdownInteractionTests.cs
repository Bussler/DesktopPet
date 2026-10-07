using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;
using static DesktopPet.Windows.Tests.NoteClipboardTestInput;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteBlockMarkdownInteractionTests
{
    [TestMethod]
    [DataRow("# ", NoteBlockKind.Heading1)]
    [DataRow("## ", NoteBlockKind.Heading2)]
    [DataRow("### ", NoteBlockKind.Heading3)]
    [DataRow("#### ", NoteBlockKind.Heading4)]
    [DataRow("##### ", NoteBlockKind.Heading5)]
    [DataRow("###### ", NoteBlockKind.Heading6)]
    public void TypedHeadingRetainsItsLevelAndEnterStartsAnOrdinaryParagraph(string prefix, NoteBlockKind kind)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, prefix);
            Pump(window);

            Assert.AreEqual(kind, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            Assert.AreEqual(string.Empty, app.GetNote(noteId).Document.GetPlainText());

            Type(editor, "Heading");
            Pump(window);
            Assert.AreEqual(kind, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            var heading = editor.Document.Blocks.OfType<Paragraph>().Single();
            Assert.AreEqual(kind, heading.Tag);
            Assert.IsTrue(kind == NoteBlockKind.Heading6 ? heading.FontSize == editor.FontSize : heading.FontSize > editor.FontSize);

            PressEnter(editor, window);
            var split = app.GetNote(noteId).Document;
            Assert.AreEqual("Heading\r\n", split.GetPlainText());
            Assert.AreEqual(kind, split.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.Paragraph, split.Paragraphs[1].Kind);

            Type(editor, "Body");
            Pump(window);
            var completed = app.GetNote(noteId).Document;
            Assert.AreEqual("Heading\r\nBody", completed.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Paragraph, completed.Paragraphs[1].Kind);
            Assert.AreEqual("Body", completed.Paragraphs[1].Runs.Single().Text);
            Assert.AreEqual(NoteTextStyle.None, completed.Paragraphs[1].Runs.Single().Style);
            var bodyRun = editor.Document.Blocks.OfType<Paragraph>().Last().Inlines.OfType<Run>().Single();
            Assert.AreEqual(editor.FontSize, bodyRun.FontSize);
            Assert.AreEqual(FontWeights.Normal, bodyRun.FontWeight);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void PastedHeadingsConvertAllLevelsAndKeepAFinalDividerEditable()
    {
        const string headings = "# one\r\n## two\r\n### three\r\n#### four\r\n##### five\r\n###### six";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            PastePlainText(editor, headings);
            Pump(window);

            var document = app.GetNote(noteId).Document;
            Assert.AreEqual("one\r\ntwo\r\nthree\r\nfour\r\nfive\r\nsix", document.GetPlainText());
            CollectionAssert.AreEqual(
                new[] { NoteBlockKind.Heading1, NoteBlockKind.Heading2, NoteBlockKind.Heading3,
                    NoteBlockKind.Heading4, NoteBlockKind.Heading5, NoteBlockKind.Heading6 },
                document.Paragraphs.Select(row => row.Kind).ToArray());

            PastePlainText(editor, "\r\n---");
            Pump(window);
            document = app.GetNote(noteId).Document;
            Assert.AreEqual(NoteBlockKind.Divider, document.Paragraphs[^2].Kind);
            Assert.AreEqual(NoteBlockKind.Paragraph, document.Paragraphs[^1].Kind);
            Assert.AreEqual(8, document.Paragraphs.Count);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void EnterCompletesDividerAndBackspaceRemovesItWithoutLosingNeighborText()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, "before");
            PressEnter(editor, window);
            Type(editor, "---");
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs[1].Kind);

            PressEnter(editor, window);
            Assert.AreEqual(NoteBlockKind.Divider, app.GetNote(noteId).Document.Paragraphs[1].Kind);
            Type(editor, "after");
            Pump(window);

            var divided = app.GetNote(noteId).Document;
            Assert.AreEqual("before\r\n\r\nafter", divided.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Divider, divided.Paragraphs[1].Kind);
            var dividerParagraph = editor.Document.Blocks.OfType<Paragraph>().Single(paragraph => paragraph.Tag is NoteBlockKind kind && kind == NoteBlockKind.Divider);
            Assert.AreEqual(1, dividerParagraph.BorderThickness.Bottom);

            var body = editor.Document.Blocks.OfType<Paragraph>().Last();
            editor.CaretPosition = body.ContentStart;
            PressBackspace(editor, window);
            Pump(window);

            var removed = app.GetNote(noteId).Document;
            Assert.AreEqual("before\r\nafter", removed.GetPlainText());
            Assert.IsFalse(removed.Paragraphs.Any(row => row.Kind == NoteBlockKind.Divider));

            RestoreHistory(window, undo: true);
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Divider, app.GetNote(noteId).Document.Paragraphs[1].Kind);
            RestoreHistory(window, undo: false);
            Pump(window);
            Assert.AreEqual("before\r\nafter", app.GetNote(noteId).Document.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("# ")]
    [DataRow("## ")]
    [DataRow("### ")]
    [DataRow("#### ")]
    [DataRow("##### ")]
    [DataRow("###### ")]
    public void DeletingAllHeadingTextRestoresOrdinaryTyping(string prefix)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, prefix);
            Pump(window);
            Type(editor, "Heading");
            Pump(window);
            var heading = editor.Document.Blocks.OfType<Paragraph>().Single();
            editor.Selection.Select(heading.ContentStart, heading.ContentEnd);
            EditingCommands.Delete.Execute(null, editor);
            Pump(window);
            Type(editor, "Replacement");
            Pump(window);
            PressEnter(editor, window);
            Type(editor, "Body");
            Pump(window);

            var document = app.GetNote(noteId).Document;
            Assert.AreEqual("Replacement\r\nBody", document.GetPlainText());
            Assert.IsTrue(document.Paragraphs.All(row => row.Kind == NoteBlockKind.Paragraph));
            foreach (var run in editor.Document.Blocks.OfType<Paragraph>().SelectMany(row => row.Inlines.OfType<Run>()))
            {
                Assert.AreEqual(editor.FontSize, run.FontSize);
                Assert.AreEqual(FontWeights.Normal, run.FontWeight);
            }
            window.ApplyAppearance(new NotesConfiguration { TextSize = 28 }, NoteColor.Yellow);
            foreach (var run in editor.Document.Blocks.OfType<Paragraph>().SelectMany(row => row.Inlines.OfType<Run>()))
                Assert.AreEqual(28d, run.FontSize);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }
    [TestMethod]
    public void DeletingElsewherePreservesANewEmptyHeading()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs =
            [
                new NoteParagraph { Kind = NoteBlockKind.Heading1 },
                new NoteParagraph { Runs = [new NoteTextRun("body")] }
            ]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var body = editor.Document.Blocks.OfType<Paragraph>().Last();
            editor.Selection.Select(body.ContentStart, body.ContentEnd);
            EditingCommands.Delete.Execute(null, editor);
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Heading1, app.GetNote(noteId).Document.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs[1].Kind);
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
        Assert.IsTrue(editor.Focus());
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(key);
        if (!key.Handled) EditingCommands.EnterParagraphBreak.Execute(null, editor);
        Pump(window);
    }

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
