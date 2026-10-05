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
public sealed class NoteListShortcutTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackspaceRemovesEmptyChecklistThenReachesPreviousParagraph(bool previousChecklist)
    {
        var source = NotesDocument.FromText("Above\r\n");
        source.Paragraphs[0].Kind = previousChecklist ? NoteBlockKind.Checklist : NoteBlockKind.Paragraph;
        source.Paragraphs[1].Kind = NoteBlockKind.Checklist;
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var list = editor.Document.Blocks.OfType<System.Windows.Documents.List>().Last();
            var empty = (Paragraph)list.ListItems.LastListItem.Blocks.FirstBlock;
            editor.CaretPosition = empty.ContentEnd;
            PressBackspace(editor, window);
            Assert.IsFalse(editor.CaretPosition.Paragraph!.Inlines.OfType<InlineUIContainer>()
                .Any(inline => inline.Child is CheckBox), "First Backspace must remove the checkbox.");
            Assert.IsNotNull(saved);
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs[1].Kind,
                "Removing the checkbox must also remove the list item structure.");
            var previous = editor.Document.Blocks.FirstBlock is Paragraph paragraph ? paragraph :
                (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock).ListItems.FirstListItem.Blocks.FirstBlock;
            PressBackspace(editor, window);
            Assert.AreEqual("Above", saved!.GetPlainText(), "Second Backspace must remove the paragraph break.");
            Assert.AreEqual(1, saved.Paragraphs.Count);
            Assert.AreSame(previous, editor.CaretPosition.Paragraph);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackspaceAtChecklistTextStartRemovesMarkerAndPreservesFormattedText(bool inTable)
    {
        var source = NotesDocument.FromText("Task").SetListKind(0, 4, NoteBlockKind.Checklist)
            .SetChecklistChecked(0, true).SetStyle(0, 4, NoteTextStyle.Bold, true);
        if (inTable) source.Paragraphs[0].Cell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var blocks = inTable ? editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0].Blocks
                : editor.Document.Blocks;
            var paragraph = (Paragraph)blocks.OfType<System.Windows.Documents.List>().Single().ListItems.FirstListItem.Blocks.FirstBlock;
            editor.CaretPosition = paragraph.Inlines.OfType<Run>().Single().ContentStart;
            PressBackspace(editor, window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("Task", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs[0].Kind);
            Assert.IsFalse(saved.Paragraphs[0].IsChecked);
            Assert.AreEqual(source.Paragraphs[0].Cell, saved.Paragraphs[0].Cell);
            CollectionAssert.AreEqual(source.Paragraphs[0].Runs, saved.Paragraphs[0].Runs);
            Type(editor, "New ");
            Pump(window);
            Assert.AreEqual("New Task", saved.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BackspaceInsideChecklistOrOverSelectedTextKeepsTheCheckbox(bool selected)
    {
        var source = NotesDocument.FromText("Task").SetListKind(0, 4, NoteBlockKind.Checklist).SetChecklistChecked(0, true);
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var paragraph = (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock).ListItems.FirstListItem.Blocks.FirstBlock;
            var run = paragraph.Inlines.OfType<Run>().Single();
            if (selected) editor.Selection.Select(run.ContentStart, run.ContentStart.GetPositionAtOffset(2)!);
            else editor.CaretPosition = run.ContentStart.GetPositionAtOffset(2)!;
            PressBackspace(editor, window);
            Assert.IsNotNull(saved);
            Assert.AreEqual(selected ? "sk" : "Tsk", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Checklist, saved.Paragraphs[0].Kind);
            Assert.IsTrue(saved.Paragraphs[0].IsChecked);
            Assert.IsTrue(paragraph.Inlines.OfType<InlineUIContainer>().Any(inline => inline.Child is CheckBox));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void RemovingChecklistWithBackspaceSupportsApplicationUndoAndRedo()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, NotesDocument.FromText("Task").SetListKind(0, 4, NoteBlockKind.Checklist)
            .SetChecklistChecked(0, true).SetStyle(0, 4, NoteTextStyle.Bold, true));
        var window = new NoteWindow(app.GetNote(id),
            (_, document) => { app.UpdateDocument(id, document); return null; }, (_, _) => null, (_, _) => null,
            (_, undo) => { var moved = undo ? app.Undo(id) : app.Redo(id); return (moved ? app.GetNote(id).Document : null, null); },
            _ => (true, null), () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var paragraph = (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock).ListItems.FirstListItem.Blocks.FirstBlock;
            editor.CaretPosition = paragraph.Inlines.OfType<Run>().Single().ContentStart;
            PressBackspace(editor, window);
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(id).Document.Paragraphs[0].Kind);
            foreach (var undo in new[] { true, false })
            {
                typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [undo]);
                Pump(window);
                var restored = app.GetNote(id).Document;
                Assert.AreEqual("Task", restored.GetPlainText());
                Assert.AreEqual(undo ? NoteBlockKind.Checklist : NoteBlockKind.Paragraph, restored.Paragraphs[0].Kind);
                Assert.AreEqual(undo, restored.Paragraphs[0].IsChecked);
                Assert.AreEqual(NoteTextStyle.Bold, restored.Paragraphs[0].Runs.Single().Style);
                Assert.AreEqual(undo, editor.Document.Blocks.FirstBlock is System.Windows.Documents.List);
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("- ", NoteBlockKind.Bulleted, "BulletsButton", false)]
    [DataRow("1) ", NoteBlockKind.Numbered, "NumberedButton", false)]
    [DataRow("- [ ] ", NoteBlockKind.Checklist, "ChecklistButton", false)]
    [DataRow("- ", NoteBlockKind.Bulleted, "BulletsButton", true)]
    [DataRow("1) ", NoteBlockKind.Numbered, "NumberedButton", true)]
    [DataRow("- [ ] ", NoteBlockKind.Checklist, "ChecklistButton", true)]
    public void TypingMarkersCreatesListsWithWorkingCaretToolbarAndContinuation(
        string marker, NoteBlockKind kind, string button, bool inTable)
    {
        var source = new NotesDocument();
        if (inTable) source.Paragraphs[0].Cell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            if (inTable)
                editor.CaretPosition = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0].ContentStart;
            foreach (var character in marker)
            {
                Type(editor, character.ToString());
                Pump(window);
            }
            Assert.IsNotNull(saved);
            Assert.AreEqual(kind, Rows().Single().Kind);
            Assert.AreEqual(string.Empty, Text());
            Assert.AreEqual(source.Paragraphs[0].Cell, saved.Paragraphs[0].Cell);
            Assert.IsTrue(((ToggleButton)window.FindName(button)).IsChecked);
            Type(editor, "Task");
            Pump(window);
            Assert.AreEqual("Task", Text());
            PressEnter(editor, window);
            Assert.AreEqual(kind, Rows().Last().Kind);
            Assert.AreEqual("Task\r\n", Text());
            PressEnter(editor, window);
            Assert.AreEqual(NoteBlockKind.Paragraph, Rows().Last().Kind);
            Type(editor, "after");
            Pump(window);
            Assert.AreEqual("Task\r\nafter", Text());
        }
        finally { window.CloseWithoutMembershipChange(); }

        IEnumerable<NoteParagraph> Rows() => saved!.Paragraphs.Where(row => row.Cell == source.Paragraphs[0].Cell);
        string Text() => new NotesDocument { Paragraphs = Rows().ToList() }.GetPlainText();
    }

    [TestMethod]
    [DataRow("- ", NoteBlockKind.Bulleted)]
    [DataRow("1) ", NoteBlockKind.Numbered)]
    [DataRow("- [ ] ", NoteBlockKind.Checklist)]
    public void ConversionSupportsApplicationUndoAndRedoWithoutReformattingLiteralMarkers(string marker, NoteBlockKind kind)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var id = app.CreateNote();
        var window = new NoteWindow(app.GetNote(id),
            (_, document) => { app.UpdateDocument(id, document); return null; }, (_, _) => null, (_, _) => null,
            (_, undo) => { var moved = undo ? app.Undo(id) : app.Redo(id); return (moved ? app.GetNote(id).Document : null, null); },
            _ => (true, null), () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, marker);
            Pump(window);
            Assert.AreEqual(kind, app.GetNote(id).Document.Paragraphs.Single().Kind);
            Restore(true);
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(id).Document.Paragraphs.Single().Kind);
            Assert.AreEqual(marker, app.GetNote(id).Document.GetPlainText());
            Restore(false);
            Pump(window);
            Assert.AreEqual(kind, app.GetNote(id).Document.Paragraphs.Single().Kind);
            Assert.AreEqual(string.Empty, app.GetNote(id).Document.GetPlainText());
            Restore(true);
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, "literal");
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(id).Document.Paragraphs.Single().Kind);
            Assert.AreEqual(marker + "literal", app.GetNote(id).Document.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }

        void Restore(bool undo) => typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [undo]);
    }

    private sealed class MemoryNotesStore : INotesStore
    {
        public NotesSnapshot? Load() => null;
        public void Save(NotesSnapshot snapshot) { }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ListMarkerAndInlineCodeFormatTogetherWithoutLosingTheCaret(bool inTable)
    {
        var source = new NotesDocument();
        if (inTable) source.Paragraphs[0].Cell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            if (inTable)
                editor.CaretPosition = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0].ContentStart;
            Type(editor, "- `value`");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual(NoteBlockKind.Bulleted, saved.Paragraphs[0].Kind);
            Assert.AreEqual(new NoteTextRun("value", NoteTextStyle.Code), saved.Paragraphs[0].Runs.Single());
            Type(editor, " after");
            Pump(window);
            Assert.AreEqual("value after", string.Concat(saved.Paragraphs[0].Runs.Select(run => run.Text)));
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[0].Runs.Last().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TypingListMarkersInsideCodeLeavesThemLiteral(bool fenced)
    {
        var source = fenced ? NotesDocument.FromText("```cs\r\n") : new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code }]
        };
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = editor.Document.ContentEnd;
            foreach (var character in "- [ ] ")
            {
                Type(editor, character.ToString());
                Pump(window);
            }
            Assert.IsNotNull(saved);
            Assert.AreEqual("- [ ] ", string.Concat(saved.Paragraphs.Last().Runs.Select(run => run.Text)));
            Assert.AreEqual(fenced ? NoteBlockKind.Paragraph : NoteBlockKind.Code, saved.Paragraphs.Last().Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void TypedChecklistCanBeClickedAndReopened()
    {
        NotesDocument? saved = null;
        var window = Create(new NotesDocument(), document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, "- [ ] Task");
            Pump(window);
            var paragraph = ((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock).ListItems.Single().Blocks.OfType<Paragraph>().Single();
            var checkbox = (CheckBox)paragraph.Inlines.OfType<InlineUIContainer>().Single().Child;
            typeof(ToggleButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(checkbox, null);
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.Paragraphs.Single().IsChecked);
            Assert.AreEqual("Task", saved.GetPlainText());
            var reopened = Create(saved, _ => { });
            try
            {
                var reopenedEditor = (RichTextBox)reopened.FindName("Editor");
                var row = ((System.Windows.Documents.List)reopenedEditor.Document.Blocks.FirstBlock).ListItems.Single().Blocks.OfType<Paragraph>().Single();
                Assert.IsTrue(((CheckBox)row.Inlines.OfType<InlineUIContainer>().Single().Child).IsChecked);
            }
            finally { reopened.CloseWithoutMembershipChange(); }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static NoteWindow Create(NotesDocument document, Action<NotesDocument> save) => new(
        new HoodNote { Document = document },
        (_, updated) => { NotesSnapshotValidator.ValidateDocument(updated); save(updated.Copy()); return null; },
        (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
        () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());

    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.Left = window.Top = -10000;
        window.Show();
        window.Activate();
        Pump(window);
    }

    private static void Pump(Window window) => window.Dispatcher.Invoke(() => { },
        System.Windows.Threading.DispatcherPriority.ApplicationIdle);

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

    private static void PressBackspace(RichTextBox editor, Window window)
    {
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Back)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(key);
        if (!key.Handled) EditingCommands.Backspace.Execute(null, editor);
        Pump(window);
    }
}
