using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopPet.Core;
using DesktopPet.Windows;
using EditorList = System.Windows.Documents.List;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteListLayoutInteractionTests
{
    [TestMethod]
    [DataRow(NoteBlockKind.Paragraph)]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Checklist)]
    [DataRow(NoteBlockKind.Numbered)]
    public void TypingLeavesUnrelatedNumberedMarkersAndLayoutUntouched(NoteBlockKind editedKind)
    {
        var source = NumberedDocument(20);
        source.Paragraphs.Add(new NoteParagraph { Runs = [new NoteTextRun("Separate")] });
        source.Paragraphs.Add(new NoteParagraph
        {
            Kind = editedKind, IsChecked = editedKind == NoteBlockKind.Checklist,
            Runs = [new NoteTextRun("Edit", NoteTextStyle.Bold)]
        });
        var app = NotesApplication.Start(new MemoryStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, source);
        var window = Create(app, id);
        try
        {
            var editor = Show(window);
            var rows = Rows(editor.Document.Blocks.OfType<EditorList>().First());
            var markers = rows.Select(row => row.Inlines.FirstInline).ToArray();
            var indents = rows.Select(row => row.TextIndent).ToArray();
            var margins = rows.Select(row => row.Margin).ToArray();
            editor.CaretPosition = editor.Document.ContentEnd;
            Pump(window);
            var changes = 0;
            editor.TextChanged += (_, _) => changes++;

            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, "x"));
            Pump(window);

            Assert.AreEqual(1, changes, "Typing one character must not generate unrelated list-formatting events.");
            CollectionAssert.AreEqual(markers, rows.Select(row => row.Inlines.FirstInline).ToArray());
            CollectionAssert.AreEqual(indents, rows.Select(row => row.TextIndent).ToArray());
            CollectionAssert.AreEqual(margins, rows.Select(row => row.Margin).ToArray());
            var saved = app.GetNote(id).Document;
            Assert.AreEqual(source.GetPlainText() + "x", saved.GetPlainText());
            Assert.AreEqual(editedKind, saved.Paragraphs[^1].Kind);
            Assert.AreEqual(editedKind == NoteBlockKind.Checklist, saved.Paragraphs[^1].IsChecked);
            Assert.IsTrue(saved.Paragraphs[^1].Runs.All(run => run.Style.HasFlag(NoteTextStyle.Bold)));
            Assert.IsTrue(app.Undo(id));
            Assert.AreEqual(source.GetPlainText(), app.GetNote(id).Text);
            Assert.IsTrue(app.Redo(id));
            Assert.AreEqual(saved.GetPlainText(), app.GetNote(id).Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("delete", "body")]
    [DataRow("backspace", "body")]
    [DataRow("replace", "body")]
    [DataRow("delete", "nested")]
    [DataRow("backspace", "nested")]
    [DataRow("replace", "nested")]
    [DataRow("delete", "table")]
    [DataRow("backspace", "table")]
    [DataRow("replace", "table")]
    public void RemovingNumberMarkerRepairsItWithoutChangingListStructure(string action, string context)
    {
        var source = NumberedDocument(20);
        source.Paragraphs.Add(new NoteParagraph { Runs = [new NoteTextRun("Separate")] });
        if (context == "nested")
            source.Paragraphs.Add(new NoteParagraph { Kind = NoteBlockKind.Bulleted, Runs = [new NoteTextRun("Parent")] });
        var targetRows = NumberedDocument(10).Paragraphs;
        var cell = context == "table" ? new NoteTableCell(Guid.NewGuid(), 0, 0) : null;
        foreach (var row in targetRows) { row.ListDepth = context == "nested" ? 1 : 0; row.Cell = cell; }
        targetRows[1].Runs.Add(new NoteTextRun("x^2", Kind: NoteInlineKind.InlineMath));
        var targetStart = source.Paragraphs.Count;
        source.Paragraphs.AddRange(targetRows);
        var app = NotesApplication.Start(new MemoryStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, source);
        var window = Create(app, id);
        try
        {
            var editor = Show(window);
            var unrelated = editor.Document.Blocks.OfType<EditorList>().First();
            var unrelatedMarkers = Rows(unrelated).Select(row => row.Inlines.FirstInline).ToArray();
            var list = context switch
            {
                "nested" => editor.Document.Blocks.OfType<EditorList>().Last().ListItems.FirstListItem
                    .Blocks.OfType<EditorList>().Single(),
                "table" => editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0]
                    .Blocks.OfType<EditorList>().Single(),
                _ => editor.Document.Blocks.OfType<EditorList>().Last()
            };
            var rowIndex = context == "body" ? 0 : context == "nested" ? 5 : 9;
            var row = Rows(list)[rowIndex];
            var width = Number(row).Width;
            var marker = (InlineUIContainer)row.Inlines.FirstInline;
            if (action == "backspace")
            {
                editor.CaretPosition = marker.ElementEnd;
                EditingCommands.Backspace.Execute(null, editor);
            }
            else
            {
                editor.Selection.Select(marker.ElementStart, action == "delete" ? marker.ElementEnd : row.ContentEnd);
                if (action == "delete") EditingCommands.Delete.Execute(null, editor);
                else TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, "x"));
            }
            Pump(window);

            Assert.IsTrue(row.Inlines.OfType<InlineUIContainer>().Any(container => container.Child is TextBlock),
                "Deleting or replacing a number marker must repair it even when paragraph structure is unchanged.");
            AssertLabels(list, 10);
            Assert.AreEqual(width, Number(row).Width);
            Assert.AreEqual(-width, row.TextIndent);
            CollectionAssert.AreEqual(unrelatedMarkers, Rows(unrelated).Select(item => item.Inlines.FirstInline).ToArray());
            var saved = app.GetNote(id).Document;
            Assert.AreEqual(source.Paragraphs.Count, saved.Paragraphs.Count);
            CollectionAssert.AreEqual(source.Paragraphs.Select(item => item.Kind).ToArray(), saved.Paragraphs.Select(item => item.Kind).ToArray());
            CollectionAssert.AreEqual(source.Paragraphs.Select(item => item.ListDepth).ToArray(), saved.Paragraphs.Select(item => item.ListDepth).ToArray());
            CollectionAssert.AreEqual(source.Paragraphs.Select(item => item.Cell).ToArray(), saved.Paragraphs.Select(item => item.Cell).ToArray());
            Assert.AreEqual(action == "replace" ? "x" : $"Item {rowIndex}", saved.Paragraphs[targetStart + rowIndex].Runs.Single().Text);
            Assert.AreEqual("x^2", saved.Paragraphs[targetStart + 1].Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);

            editor.CaretPosition = row.ContentEnd;
            var changes = 0;
            editor.TextChanged += (_, _) => changes++;
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, "y"));
            Pump(window);
            Assert.AreEqual(1, changes, "Typing after marker repair must still avoid unrelated formatting events.");
            Assert.AreEqual(action == "replace" ? "xy" : $"Item {rowIndex}y", app.GetNote(id).Document.Paragraphs[targetStart + rowIndex].Runs.Single().Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void RefreshingUnchangedNumberedLayoutDoesNotGenerateTextChanges()
    {
        var app = NotesApplication.Start(new MemoryStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, NumberedDocument(20));
        var window = Create(app, id);
        try
        {
            var editor = Show(window);
            var changes = 0;
            editor.TextChanged += (_, _) => changes++;
            var updating = typeof(NoteWindow).GetField("_updatingEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
            updating.SetValue(window, true);
            try
            {
                typeof(NoteWindow).GetMethod("RefreshListLayout", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, null);
            }
            finally { updating.SetValue(window, false); }
            Assert.AreEqual(0, changes, "An unchanged layout must not change paragraph indents away and back.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void RefreshingNumberedLayoutPreservesEmbeddedMath()
    {
        var app = NotesApplication.Start(new MemoryStore());
        var id = app.CreateNote();
        var source = NumberedDocument(10);
        source.Paragraphs[0].Runs.Add(new NoteTextRun("x^2", Kind: NoteInlineKind.InlineMath));
        app.UpdateDocument(id, source);
        var window = Create(app, id);
        try
        {
            var editor = Show(window);
            var list = editor.Document.Blocks.OfType<EditorList>().Single();
            var math = Rows(list)[0].Inlines.OfType<InlineUIContainer>()
                .Single(container => container.Child.GetType().Name == "NoteInlineMathButton");
            editor.CaretPosition = Rows(list)[^1].ContentEnd;
            EditingCommands.EnterParagraphBreak.Execute(null, editor);
            Pump(window);
            Assert.IsTrue(Rows(list)[0].Inlines.Contains(math));
            Assert.AreEqual("x^2", app.GetNote(id).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.InlineMath).Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void NativeItemInsertionAndRemovalRefreshNumberingAndMarkerWidth()
    {
        var app = NotesApplication.Start(new MemoryStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, NumberedDocument(9));
        var window = Create(app, id);
        try
        {
            var editor = Show(window);
            var list = editor.Document.Blocks.OfType<EditorList>().Single();
            var originalWidth = Number(Rows(list)[0]).Width;
            editor.CaretPosition = Rows(list)[^1].ContentEnd;

            EditingCommands.EnterParagraphBreak.Execute(null, editor);
            Pump(window);

            Assert.AreEqual(10, app.GetNote(id).Document.Paragraphs.Count);
            AssertLabels(list, 10);
            var widened = Number(Rows(list)[0]).Width;
            Assert.IsTrue(widened > originalWidth, "Crossing nine to ten items must widen every number marker.");
            Assert.IsTrue(Rows(list).All(row => Number(row).Width == widened && row.TextIndent == -widened));

            editor.BeginChange();
            try { list.ListItems.Remove(list.ListItems.LastListItem); }
            finally { editor.EndChange(); }
            Pump(window);

            Assert.AreEqual(9, app.GetNote(id).Document.Paragraphs.Count);
            AssertLabels(list, 9);
            Assert.AreEqual(originalWidth, Number(Rows(list)[0]).Width);
            window.ApplyAppearance(new NotesConfiguration { TextSize = 24, LineSpacing = 1.5 }, NoteColor.Yellow);
            Assert.IsTrue(Number(Rows(list)[0]).Width > originalWidth);
            Assert.IsTrue(Rows(list).All(row => row.Margin.Bottom == 6));
            window.ApplyAppearance(new NotesConfiguration(), NoteColor.Yellow);
            Assert.AreEqual(originalWidth, Number(Rows(list)[0]).Width);
            Assert.IsTrue(Rows(list).All(row => row.Margin.Bottom == 8));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static NotesDocument NumberedDocument(int count) => new()
    {
        Paragraphs = Enumerable.Range(0, count).Select(index => new NoteParagraph
        { Kind = NoteBlockKind.Numbered, Runs = [new NoteTextRun($"Item {index}")] }).ToList()
    };

    private static Paragraph[] Rows(EditorList list) => list.ListItems
        .SelectMany(item => item.Blocks.OfType<Paragraph>()).ToArray();
    private static TextBlock Number(Paragraph row) => (TextBlock)row.Inlines.OfType<InlineUIContainer>()
        .Single(container => container.Child is TextBlock).Child;
    private static void AssertLabels(EditorList list, int count) => CollectionAssert.AreEqual(
        Enumerable.Range(1, count).Select(index => $"{index}.").ToArray(), Rows(list).Select(row => Number(row).Text).ToArray());
    private static void Pump(Window window) => window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static RichTextBox Show(NoteWindow window)
    {
        var editor = (RichTextBox)window.FindName("Editor");
        CommandManager.AddPreviewCanExecuteHandler(editor, (_, args) =>
        {
            if (args.Command != ApplicationCommands.Paste) return;
            args.CanExecute = true;
            args.Handled = true;
        });
        window.Opacity = 0;
        window.Left = window.Top = -10000;
        window.Show();
        window.Activate();
        Assert.IsTrue(editor.Focus());
        Pump(window);
        return editor;
    }

    private static NoteWindow Create(NotesApplication app, Guid id) => new(app.GetNote(id),
        (_, document) => { app.UpdateDocument(id, document); return app.LastSaveError; },
        (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
        () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
    private sealed class MemoryStore : INotesStore
    { public NotesSnapshot? Load() => null; public void Save(NotesSnapshot snapshot) { } }
}
