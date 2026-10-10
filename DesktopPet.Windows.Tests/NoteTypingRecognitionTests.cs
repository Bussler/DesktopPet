using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteTypingRecognitionTests
{
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [TestMethod]
    public void RecognitionPlanIgnoresOldMarkersAndSelectsOnlyAffectedPasses()
    {
        var document = CreateLongDocument("open ");
        var original = document.GetPlainText();
        var changed = document.Copy();
        changed.Paragraphs[^1].Runs = [new NoteTextRun("open x")];
        Assert.AreEqual("None", SelectPasses(changed, original, changed.GetPlainText(), (original.Length, 1)));

        const string unfinished = "**word*";
        Assert.AreEqual("Emphasis", SelectPasses(NotesDocument.FromText(unfinished), string.Empty, unfinished,
            (0, unfinished.Length)));
        Assert.AreEqual("Emphasis", SelectPasses(NotesDocument.FromText("**word*"), "**word**", "**word*", (7, 0)));
        Assert.AreEqual("Block", SelectPasses(NotesDocument.FromText("# "), "#", "# ", (1, 1)));
        Assert.AreEqual("None", SelectPasses(NotesDocument.FromText("https://example.com/#"),
            "https://example.com/", "https://example.com/#", (20, 1), pendingUrlEdit: true));
        Assert.AreEqual("Link", SelectPasses(NotesDocument.FromText("https://example.com "),
            "https://example.com", "https://example.com ", (19, 1), completedPlainUrl: true));
        Assert.AreEqual("All", SelectPasses(NotesDocument.FromText("ordinary paste"), "", "ordinary paste",
            (0, 14), forceAll: true));
    }

    [TestMethod]
    public void OrdinaryTypingDoesNotQueueRecognitionForAnUnrelatedMarker()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var document = CreateLongDocument("open ");
        app.UpdateDocument(noteId, document);
        var window = CreateWindow(app, noteId);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, "x");

            Pump(window);

            var accepted = app.GetNote(noteId).Document;
            Assert.IsTrue(accepted.GetPlainText().EndsWith("open x", StringComparison.Ordinal));
            Assert.IsTrue(accepted.Paragraphs[0].Runs.Single().Text.Contains("ordinary_identifier", StringComparison.Ordinal));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CompletingALiteralChecklistPrefixConvertsAndCanBeUndone(bool undoRestored)
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, NotesDocument.FromText("- [ ]"));
        var window = CreateWindow(app, noteId);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            if (undoRestored)
            {
                editor.CaretPosition = editor.Document.ContentEnd;
                Type(editor, "x");
                Pump(window);
                typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [true]);
                Pump(window);
                Assert.AreEqual("- [ ]", app.GetNote(noteId).Text);
            }
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, " ");
            Pump(window);

            Assert.AreEqual(NoteBlockKind.Checklist, app.GetNote(noteId).Document.Paragraphs.Single().Kind);
            Assert.AreEqual(string.Empty, app.GetNote(noteId).Text);
            typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [true]);
            Pump(window);
            Assert.AreEqual("- [ ] ", app.GetNote(noteId).Text);
            Assert.AreEqual(NoteBlockKind.Paragraph, app.GetNote(noteId).Document.Paragraphs.Single().Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void AlphabeticPlainUrlCompletionStillConvertsBesideAnUnrelatedMarker()
    {
        const string url = "https://example.com/path?q=1#part";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, CreateLongDocument("open "));
        var window = CreateWindow(app, noteId);
        try
        {
            Show(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, url);
            Pump(window);
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link));

            Type(editor, " ");
            Pump(window);

            var link = app.GetNote(noteId).Document.Paragraphs[^1].Runs.Single(run => run.Kind == NoteInlineKind.Link);
            Assert.AreEqual(url, link.Text);
            Assert.AreEqual(url, link.LinkDestination);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static NotesDocument CreateLongDocument(string finalText) => new()
    {
        Paragraphs = Enumerable.Range(0, 100).Select(index => new NoteParagraph
        {
            Runs = [new NoteTextRun(index == 0
                ? "This ordinary_identifier is literal text, unrelated to the edit."
                : index == 99 ? finalText : "A repeatable ordinary paragraph for the long note.")]
        }).ToList()
    };

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

    private static void Show(NoteWindow window)
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
    }

    private static void Type(RichTextBox editor, string text) =>
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, text));

    private static string SelectPasses(NotesDocument document, string original, string text,
        (int Start, int Length) input, bool forceAll = false, bool completedPlainUrl = false,
        bool pendingUrlEdit = false) => typeof(NoteWindow).GetMethod("SelectMarkupPasses", PrivateStatic)!
        .Invoke(null, [document, original, text, input, forceAll, completedPlainUrl, pendingUrlEdit])!.ToString()!;

    private static void Pump(Window window) =>
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private sealed class MemoryNotesStore : INotesStore
    {
        public NotesSnapshot? Load() => null;
        public void Save(NotesSnapshot snapshot) { }
    }
}
