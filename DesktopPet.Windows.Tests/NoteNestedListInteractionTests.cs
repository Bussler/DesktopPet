using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;
using EditorList = System.Windows.Documents.List;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteNestedListInteractionTests
{
    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Checklist)]
    public void EnterContinuesNestedItemsThenOutdentsOneLevelAtATime(NoteBlockKind kind)
    {
        var source = NotesDocument.FromText("Parent\r\nChild\r\nTask\r\nAfter");
        var depths = new[] { 0, 1, 2, 0 };
        for (var index = 0; index < source.Paragraphs.Count; index++)
        {
            source.Paragraphs[index].Kind = kind;
            source.Paragraphs[index].ListDepth = depths[index];
        }
        source.Paragraphs[2].IsChecked = kind == NoteBlockKind.Checklist;
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.Focus();
            editor.CaretPosition = Row(editor, "Task").ContentEnd;
            foreach (var (expectedDepth, expectedKind) in new[] { (2, kind), (1, kind), (0, kind), (0, NoteBlockKind.Paragraph) })
            {
                PressEnter(editor);
                Assert.IsNotNull(saved);
                Assert.AreEqual("Parent\r\nChild\r\nTask\r\n\r\nAfter", saved.GetPlainText());
                CollectionAssert.AreEqual(new[] { 0, 1, 2, expectedDepth, 0 }, saved.Paragraphs.Select(row => row.ListDepth).ToArray());
                Assert.AreEqual(expectedKind, saved.Paragraphs[3].Kind);
                Assert.IsFalse(saved.Paragraphs[3].IsChecked);
                Assert.AreEqual(kind == NoteBlockKind.Checklist, saved.Paragraphs[2].IsChecked);
                Assert.AreSame(Row(editor, string.Empty), editor.CaretPosition.Paragraph);
                if (expectedKind == NoteBlockKind.Checklist)
                    Assert.IsFalse(((CheckBox)Row(editor, string.Empty).Inlines.OfType<InlineUIContainer>().Single().Child).IsChecked);
                else if (expectedKind == NoteBlockKind.Numbered)
                    Assert.AreEqual("2.", ((TextBlock)Row(editor, string.Empty).Inlines.OfType<InlineUIContainer>().Single().Child).Text);
            }
            Assert.AreEqual(NoteBlockKind.Paragraph, saved!.Paragraphs[3].Kind);
            var reopened = Create(saved, _ => { });
            try
            {
                Show(reopened);
                var restoredEditor = (RichTextBox)reopened.FindName("Editor");
                Assert.IsTrue(Row(restoredEditor, "Task").Parent is ListItem { Parent: EditorList { Parent: ListItem } });
                Assert.IsTrue(Row(restoredEditor, string.Empty).Parent is FlowDocument);
            }
            finally { reopened.CloseWithoutMembershipChange(); }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Numbered, NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Checklist, NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Bulleted, NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Checklist, NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Bulleted, NoteBlockKind.Checklist)]
    [DataRow(NoteBlockKind.Numbered, NoteBlockKind.Checklist)]
    public void EnterOnEmptyNestedItemJoinsTheParentList(NoteBlockKind outer, NoteBlockKind child)
    {
        var source = NotesDocument.FromText("Parent\r\nTask\r\nAfter");
        foreach (var row in source.Paragraphs) row.Kind = outer;
        source.Paragraphs[1].Kind = child;
        source.Paragraphs[1].ListDepth = 1;
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.Focus();
            editor.CaretPosition = Row(editor, "Task").ContentEnd;
            PressEnter(editor);
            PressEnter(editor);
            Assert.IsNotNull(saved);
            Assert.AreEqual("Parent\r\nTask\r\n\r\nAfter", saved.GetPlainText());
            CollectionAssert.AreEqual(new[] { outer, child, outer, outer }, saved.Paragraphs.Select(row => row.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 0 }, saved.Paragraphs.Select(row => row.ListDepth).ToArray());
            Assert.AreSame(Row(editor, string.Empty), editor.CaretPosition.Paragraph);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Checklist)]
    public void IndentingListItemPreservesNestingAndContentAcrossSaveAndReopen(NoteBlockKind kind)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"HoodNestedList-{Guid.NewGuid():N}.json");
        var app = NotesApplication.Start(new JsonNotesStore(path));
        var id = app.CreateNote();
        var source = NotesDocument.FromText("Above\r\nBefore\r\nNested\r\nAfter");
        foreach (var row in source.Paragraphs) row.Kind = kind;
        source.Paragraphs[2].Runs = [new NoteTextRun("Nested", NoteTextStyle.Bold)];
        source.Paragraphs[2].IsChecked = kind == NoteBlockKind.Checklist;
        app.UpdateDocument(id, source);
        var window = Create(source, document => app.UpdateDocument(id, document), undo =>
            (undo ? app.Undo(id) : app.Redo(id)) ? app.GetNote(id).Document : null);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Indent(editor);
            Assert.AreEqual(source.GetPlainText(), app.GetNote(id).Document.GetPlainText(),
                "Indentation must preserve the text and paragraph boundaries.");
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 0 }, app.GetNote(id).Document.Paragraphs.Select(row => row.ListDepth).ToArray());
            foreach (var undo in new[] { true, false })
            {
                typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [undo]);
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.AreEqual(undo ? 0 : 1, app.GetNote(id).Document.Paragraphs[2].ListDepth);
            }
            var originalX = Row(editor, "Nested").ContentStart.GetCharacterRect(LogicalDirection.Forward).X;
            window.CloseWithoutMembershipChange();
            app.Flush();
            var restored = NotesApplication.Start(new JsonNotesStore(path)).GetNote(id).Document;
            Assert.AreEqual(NoteTextStyle.Bold, restored.Paragraphs[2].Runs.Single().Style);
            Assert.AreEqual(kind == NoteBlockKind.Checklist, restored.Paragraphs[2].IsChecked);
            var reopened = Create(restored, _ => { });
            try
            {
                Show(reopened);
                var restoredEditor = (RichTextBox)reopened.FindName("Editor");
                Assert.AreEqual(originalX, Row(restoredEditor, "Nested").ContentStart
                    .GetCharacterRect(LogicalDirection.Forward).X, 0.1, "Reopening must retain the nested item's indentation.");
                Assert.IsTrue(Row(restoredEditor, "Nested").Parent is ListItem { Parent: EditorList { Parent: ListItem } },
                    "Reopening must reconstruct a nested list.");
            }
            finally { reopened.CloseWithoutMembershipChange(); }
        }
        finally { window.CloseWithoutMembershipChange(); System.IO.File.Delete(path); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Checklist)]
    public void NestedListGapsMatchSiblingGapsDuringSpacingPreview(NoteBlockKind kind)
    {
        var source = NotesDocument.FromText("Above\r\nBefore\r\nNested\r\nAfter");
        foreach (var row in source.Paragraphs) row.Kind = kind;
        var window = Create(source, _ => { });
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Indent(editor);
            foreach (var spacing in new[] { 2d, 1d, 3d, 2d })
            {
                window.ApplyAppearance(new NotesConfiguration { LineSpacing = spacing }, NoteColor.Yellow);
                editor.UpdateLayout();
                var expected = Y("Before") - Y("Above");
                Assert.AreEqual(expected, Y("Nested") - Y("Before"), 0.1,
                    $"The gap before the nested item must match sibling spacing at {spacing}.");
                Assert.AreEqual(expected, Y("After") - Y("Nested"), 0.1,
                    $"The gap after the nested item must match sibling spacing at {spacing}.");
            }
            double Y(string text) => Row(editor, text).ContentStart.GetCharacterRect(LogicalDirection.Forward).Y;
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted, false)]
    [DataRow(NoteBlockKind.Numbered, false)]
    [DataRow(NoteBlockKind.Checklist, false)]
    [DataRow(NoteBlockKind.Bulleted, true)]
    [DataRow(NoteBlockKind.Numbered, true)]
    [DataRow(NoteBlockKind.Checklist, true)]
    public void DeeplyNestedListsSupportContinuationAndOutdent(NoteBlockKind kind, bool enter)
    {
        var source = NotesDocument.FromText("Parent\r\nChild\r\nGrandchild\r\nSibling\r\nAfter");
        var depths = new[] { 0, 1, 2, 1, 0 };
        for (var index = 0; index < source.Paragraphs.Count; index++)
        {
            source.Paragraphs[index].Kind = kind;
            source.Paragraphs[index].ListDepth = depths[index];
        }
        NotesDocument? saved = null;
        var window = Create(source, document => saved = document);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.Focus();
            var grandchild = Row(editor, "Grandchild");
            Assert.IsTrue(X("Grandchild") > X("Child") && X("Child") > X("Parent"));
            editor.CaretPosition = grandchild.ContentEnd;
            if (enter)
            {
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                editor.RaiseEvent(key);
                if (!key.Handled) EditingCommands.EnterParagraphBreak.Execute(null, editor);
            }
            else EditingCommands.DecreaseIndentation.Execute(null, editor);
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.IsNotNull(saved);
            Assert.AreEqual(enter ? "Parent\r\nChild\r\nGrandchild\r\n\r\nSibling\r\nAfter" : source.GetPlainText(), saved.GetPlainText());
            CollectionAssert.AreEqual(enter ? new[] { 0, 1, 2, 2, 1, 0 } : new[] { 0, 1, 1, 1, 0 },
                saved.Paragraphs.Select(row => row.ListDepth).ToArray());
            Assert.IsTrue(saved.Paragraphs.All(row => row.Kind == kind));
            double X(string text) => Row(editor, text).ContentStart.GetCharacterRect(LogicalDirection.Forward).X;
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Checklist)]
    public void RemovingParentListFormattingRetainsChildIndentationAndFollowingMarker(NoteBlockKind kind)
    {
        var source = NotesDocument.FromText("Above\r\nBefore\r\nParent\r\nNested\r\nAfter");
        foreach (var row in source.Paragraphs) row.Kind = kind;
        source.Paragraphs[3].ListDepth = 1;
        var window = Create(source, _ => { });
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.Focus();
            editor.CaretPosition = Row(editor, "Parent").Inlines.OfType<Run>().Single().ContentStart;
            ClickListButton(window, kind);
            var followingList = (EditorList)((ListItem)Row(editor, "After").Parent).Parent;
            Assert.AreEqual(kind == NoteBlockKind.Bulleted ? TextMarkerStyle.Disc : TextMarkerStyle.None, followingList.MarkerStyle);
            var restored = (NotesDocument)typeof(NoteWindow).GetMethod("ReadEditorDocument", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, null)!;
            Assert.AreEqual(source.GetPlainText(), restored.GetPlainText());
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 0 }, restored.Paragraphs.Select(row => row.ListDepth).ToArray());
            foreach (var spacing in new[] { 2d, 1d, 3d, 2d })
            {
                window.ApplyAppearance(new NotesConfiguration { LineSpacing = spacing }, NoteColor.Yellow);
                editor.UpdateLayout();
                Assert.AreEqual(Y("Before") - Y("Above"), Y("After") - Y("Nested"), 0.1,
                    $"Removing parent formatting must retain configured spacing at {spacing}.");
            }
            double Y(string text) => Row(editor, text).ContentStart.GetCharacterRect(LogicalDirection.Forward).Y;
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted, NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Bulleted, NoteBlockKind.Checklist)]
    [DataRow(NoteBlockKind.Numbered, NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered, NoteBlockKind.Checklist)]
    [DataRow(NoteBlockKind.Checklist, NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Checklist, NoteBlockKind.Numbered)]
    public void CtrlEnterExitsTheWholeMixedListAndPreservesTheAdjacentList(NoteBlockKind outer, NoteBlockKind child)
    {
        foreach (var caretText in new[] { "Parent", "Nested", "After" })
        {
            var source = NotesDocument.FromText("Parent\r\nNested\r\nAfter\r\nNext");
            foreach (var row in source.Paragraphs) row.Kind = outer;
            source.Paragraphs[1].ListDepth = 1;
            source.Paragraphs[3].Kind = child;
            NotesDocument? saved = null;
            var window = Create(source, document => saved = document);
            try
            {
                Show(window);
                var editor = (RichTextBox)window.FindName("Editor");
                editor.Focus();
                editor.CaretPosition = Row(editor, "Nested").Inlines.OfType<Run>().Single().ContentStart;
                ClickListButton(window, child);
                editor.CaretPosition = Row(editor, caretText).ContentEnd;
                Assert.IsTrue((bool)typeof(NoteWindow).GetMethod("HandleEditorKey", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [Key.Enter, true, false])!);
                Assert.IsNotNull(saved);
                Assert.AreEqual("Parent\r\nNested\r\nAfter\r\n\r\nNext", saved.GetPlainText(), $"Caret in {caretText} must exit the outer list.");
                CollectionAssert.AreEqual(new[] { outer, child, outer, NoteBlockKind.Paragraph, child },
                    saved.Paragraphs.Select(row => row.Kind).ToArray());
                CollectionAssert.AreEqual(new[] { 0, 1, 0, 0, 0 }, saved.Paragraphs.Select(row => row.ListDepth).ToArray());
                Assert.AreSame(Row(editor, string.Empty), editor.CaretPosition.Paragraph);
            }
            finally { window.CloseWithoutMembershipChange(); }
        }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered)]
    [DataRow(NoteBlockKind.Checklist)]
    public void SupportedDepthBoundaryRendersAndExcessiveDepthIsRejected(NoteBlockKind kind)
    {
        var source = NotesDocument.FromText("Task").SetListKind(0, 4, kind);
        source.Paragraphs[0].ListDepth = NotesSnapshotValidator.MaximumListDepth;
        var window = Create(source, _ => { });
        try
        {
            Show(window);
            var restored = (NotesDocument)typeof(NoteWindow).GetMethod("ReadEditorDocument", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, null)!;
            Assert.AreEqual("Task", restored.GetPlainText());
            Assert.AreEqual(NotesSnapshotValidator.MaximumListDepth, restored.Paragraphs.Single().ListDepth);
        }
        finally { window.CloseWithoutMembershipChange(); }
        source.Paragraphs[0].ListDepth++;
        Assert.ThrowsExactly<System.IO.InvalidDataException>(() => Create(source, _ => { }));
    }

    [TestMethod]
    public void UnsafeClipboardDepthLeavesTheCurrentNoteUntouched()
    {
        var saves = 0;
        var window = Create(NotesDocument.FromText("Existing"), _ => saves++);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var payload = NotesDocument.FromText("Task").SetListKind(0, 4, NoteBlockKind.Bulleted);
            payload.Paragraphs[0].ListDepth = NotesSnapshotValidator.MaximumListDepth + 1;
            var data = new System.Windows.DataObject(DataFormats.UnicodeText, "Task");
            data.SetData("Hood.Notes.List.v1", JsonSerializer.Serialize(payload));
            NoteClipboardTestInput.Paste(editor, data);
            Assert.AreEqual(0, saves, "Unsafe nesting must be rejected before the deferred rebuild.");
            Assert.AreEqual("Existing", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.TrimEnd('\r', '\n'));
            StringAssert.Contains(((TextBlock)window.FindName("SaveStatus")).Text, "indentation");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static void ClickListButton(NoteWindow window, NoteBlockKind kind) =>
        ((ToggleButton)window.FindName(kind switch
        {
            NoteBlockKind.Bulleted => "BulletsButton",
            NoteBlockKind.Numbered => "NumberedButton",
            _ => "ChecklistButton"
        })).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Indent(RichTextBox editor)
    {
        editor.Focus();
        editor.CaretPosition = Row(editor, "Nested").Inlines.OfType<Run>().Single().ContentStart;
        EditingCommands.IncreaseIndentation.Execute(null, editor);
        editor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        editor.UpdateLayout();
        Assert.IsTrue(Row(editor, "Nested").Parent is ListItem { Parent: EditorList { Parent: ListItem } });
    }

    private static void PressEnter(RichTextBox editor)
    {
        var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        editor.RaiseEvent(key);
        if (!key.Handled) EditingCommands.EnterParagraphBreak.Execute(null, editor);
        editor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static Paragraph Row(RichTextBox editor, string text) => Paragraphs(editor.Document.Blocks)
        .Single(row => string.Concat(row.Inlines.OfType<Run>().Select(run => run.Text)) == text);

    private static IEnumerable<Paragraph> Paragraphs(BlockCollection blocks)
    {
        foreach (var block in blocks)
            if (block is Paragraph paragraph) yield return paragraph;
            else if (block is EditorList list)
                foreach (var item in list.ListItems)
                    foreach (var nested in Paragraphs(item.Blocks)) yield return nested;
    }

    private static NoteWindow Create(NotesDocument document, Action<NotesDocument> save, Func<bool, NotesDocument?>? history = null) => new(
        new HoodNote { Document = document }, (_, updated) => { save(updated.Copy()); return null; },
        (_, _) => null, (_, _) => null, (_, undo) => (history?.Invoke(undo), null), _ => (true, null),
        () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());

    private static void Show(NoteWindow window)
    {
        window.Opacity = 0;
        window.Left = window.Top = -10000;
        window.Show();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }
}
