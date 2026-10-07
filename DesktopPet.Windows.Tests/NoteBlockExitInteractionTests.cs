using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteBlockExitInteractionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CtrlEnterAfterTabbingToRenderedMathUsesFocusedBlockInsteadOfRetainedCaret(bool caretAfterMath)
    {
        var source = new NotesDocument { Paragraphs = [
            new NoteParagraph { Runs = [new NoteTextRun("before")] },
            new NoteParagraph { Kind = NoteBlockKind.DisplayMath, Runs = [new NoteTextRun("$$x^2$$")] },
            new NoteParagraph { Runs = [new NoteTextRun("after")] }
        ] };
        NotesDocument? saved = null;
        var window = new NoteWindow(new HoodNote { Document = source },
            (_, document) => { NotesSnapshotValidator.ValidateDocument(document); saved = document.Copy(); return null; },
            (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            window.Opacity = 0;
            window.Left = window.Top = -10000;
            window.Show();
            window.Activate();
            Pump(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var paragraphs = editor.Document.Blocks.OfType<Paragraph>().ToArray();
            var retainedCaretParagraph = paragraphs[caretAfterMath ? 2 : 0];
            editor.CaretPosition = retainedCaretParagraph.ContentStart;
            var math = paragraphs[1].Inlines.OfType<InlineUIContainer>().Single().Child;
            Assert.IsTrue(editor.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.AreSame(math, Keyboard.FocusedElement);
            Assert.IsTrue(editor.IsKeyboardFocusWithin);
            Assert.AreSame(retainedCaretParagraph, editor.CaretPosition.Paragraph);

            Assert.IsTrue(Handle(window, Key.Enter, true));
            Assert.IsNotNull(saved);
            CollectionAssert.AreEqual(new[] { NoteBlockKind.Paragraph, NoteBlockKind.DisplayMath,
                NoteBlockKind.Paragraph, NoteBlockKind.Paragraph }, saved.Paragraphs.Select(row => row.Kind).ToArray());
            Assert.AreEqual("before\r\n$$x^2$$\r\n\r\nafter", saved.GetPlainText());
            Assert.IsTrue(editor.IsKeyboardFocused);
            Assert.AreSame(editor.Document.Blocks.ElementAt(2), editor.CaretPosition.Paragraph);
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, "below"));
            Pump(window);
            Assert.AreEqual("before\r\n$$x^2$$\r\nbelow\r\nafter", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[2].Runs.Single().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Code, false)]
    [DataRow(NoteBlockKind.Code, true)]
    [DataRow(NoteBlockKind.DisplayMath, false)]
    [DataRow(NoteBlockKind.DisplayMath, true)]
    [DataRow(NoteBlockKind.Paragraph, false)]
    [DataRow(NoteBlockKind.Heading2, false)]
    [DataRow(NoteBlockKind.Bulleted, false)]
    [DataRow(NoteBlockKind.Numbered, false)]
    [DataRow(NoteBlockKind.Checklist, false)]
    [DataRow(NoteBlockKind.Divider, false)]
    public void CtrlEnterPreservesElementAndNeighborsAndCreatesPlainParagraphWithUndoRedo(NoteBlockKind kind, bool select)
    {
        var source = new NotesDocument { Paragraphs = [
            new NoteParagraph { Kind = kind, Runs = kind == NoteBlockKind.Divider ? [] :
                [new NoteTextRun(kind == NoteBlockKind.DisplayMath ? "$$x^2$$" : "content",
                    kind == NoteBlockKind.DisplayMath ? NoteTextStyle.None : NoteTextStyle.Bold)] },
            new NoteParagraph { Runs = [new NoteTextRun("neighbor", NoteTextStyle.Italic)] }
        ] };
        if (kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist)
            source.Paragraphs.Insert(1, new NoteParagraph { Kind = kind, Runs = [new NoteTextRun("second")] });
        var app = NotesApplication.Start(new MemoryNotesStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, source);
        var window = new NoteWindow(app.GetNote(id),
            (_, document) => { app.UpdateDocument(id, document); return null; }, (_, _) => null, (_, _) => null,
            (_, undo) => { var moved = undo ? app.Undo(id) : app.Redo(id); return (moved ? app.GetNote(id).Document : null, null); },
            _ => (true, null), () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            window.Opacity = 0;
            window.Left = window.Top = -10000;
            window.Show();
            window.Activate();
            Pump(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var paragraph = kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist
                ? (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock).ListItems.FirstListItem.Blocks.FirstBlock
                : (Paragraph)editor.Document.Blocks.FirstBlock;
            editor.CaretPosition = paragraph.ContentStart;
            if (kind == NoteBlockKind.DisplayMath && select)
            {
                ((Button)paragraph.Inlines.OfType<InlineUIContainer>().Single().Child).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Pump(window);
            }
            if (select) editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
            var before = app.GetNote(id).Document;
            Assert.IsTrue(Handle(window, Key.Enter, true));
            var updated = app.GetNote(id).Document;
            var insertion = source.Paragraphs.Count - 1;
            Assert.AreEqual(source.Paragraphs.Count + 1, updated.Paragraphs.Count);
            Assert.AreEqual(NoteBlockKind.Paragraph, updated.Paragraphs[insertion].Kind);
            Assert.IsNull(updated.Paragraphs[insertion].Cell);
            Assert.AreEqual(0, updated.Paragraphs[insertion].Runs.Count);
            CollectionAssert.AreEqual(before.Paragraphs[0].Runs.ToArray(), updated.Paragraphs[0].Runs.ToArray());
            CollectionAssert.AreEqual(before.Paragraphs.Last().Runs.ToArray(), updated.Paragraphs.Last().Runs.ToArray());
            Assert.IsTrue(editor.Selection.IsEmpty);
            Assert.IsTrue(editor.IsKeyboardFocusWithin);
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, "below"));
            Pump(window);
            var typed = app.GetNote(id).Document;
            Assert.AreEqual("below", typed.Paragraphs[insertion].Runs.Single().Text);
            Assert.AreEqual(NoteTextStyle.None, typed.Paragraphs[insertion].Runs.Single().Style);
            Assert.IsTrue(Handle(window, Key.Z, true));
            Assert.AreEqual(updated.GetPlainText(), app.GetNote(id).Document.GetPlainText());
            Assert.IsTrue(Handle(window, Key.Z, true));
            Assert.AreEqual(before.GetPlainText(), app.GetNote(id).Document.GetPlainText());
            Assert.AreEqual(source.Paragraphs.Count, app.GetNote(id).Document.Paragraphs.Count);
            Assert.IsTrue(Handle(window, Key.Y, true));
            Assert.AreEqual(updated.GetPlainText(), app.GetNote(id).Document.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CtrlEnterAtCodeTextLimitKeepsDocumentAndSelection()
    {
        var source = new NotesDocument { Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code,
            Runs = [new NoteTextRun(new string('x', NotesSnapshotValidator.MaximumTextCharacters))] }] };
        var saves = 0;
        var window = new NoteWindow(new HoodNote { Document = source }, (_, _) => { saves++; return null; },
            (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var accepted = editor.Document;
            var paragraph = (Paragraph)accepted.Blocks.FirstBlock;
            editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
            Assert.IsTrue(Handle(window, Key.Enter, true));
            Assert.AreSame(accepted, editor.Document);
            Assert.IsFalse(editor.Selection.IsEmpty);
            Assert.AreEqual(0, saves);
            Assert.IsFalse(string.IsNullOrEmpty(((TextBlock)window.FindName("SaveStatus")).Text));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static bool Handle(NoteWindow window, Key key, bool control) =>
        (bool)typeof(NoteWindow).GetMethod("HandleEditorKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [key, control, false])!;
    private static void Pump(Window window) => window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    private sealed class MemoryNotesStore : INotesStore
    {
        public NotesSnapshot? Load() => null;
        public void Save(NotesSnapshot snapshot) { }
    }
}
