using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using DesktopPet.Core;
using DesktopPet.Windows;
using static DesktopPet.Windows.Tests.NoteClipboardTestInput;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteLinkInteractionTests
{
    [TestMethod]
    public void PastedLinksRenderCopyAndPasteSemanticallyAndActivateThroughTheBrowserBoundary()
    {
        const string firstDestination = "https://example.com/docs?q=one#part";
        const string failedDestination = "https://example.com/unavailable";
        const string source = "[Guide](" + firstDestination + ") [Unavailable](" + failedDestination + ")";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var attempts = new List<string>();
        var sourceWindow = CreateWindow(app, noteId, destination =>
        {
            attempts.Add(destination);
            if (destination == failedDestination) throw new InvalidOperationException("No browser is available.");
        });
        var targetApp = NotesApplication.Start(new MemoryNotesStore());
        var targetId = targetApp.CreateNote();
        var targetWindow = CreateWindow(targetApp, targetId);
        try
        {
            ShowHidden(sourceWindow);
            ShowHidden(targetWindow);
            var sourceEditor = (RichTextBox)sourceWindow.FindName("Editor");
            PastePlainText(sourceEditor, source);
            Pump(sourceWindow);

            var document = app.GetNote(noteId).Document;
            Assert.AreEqual("Guide Unavailable", document.GetPlainText());
            CollectionAssert.AreEquivalent(new[] { firstDestination, failedDestination },
                document.Paragraphs.SelectMany(row => row.Runs).Where(run => run.Kind == NoteInlineKind.Link)
                    .Select(run => run.LinkDestination).ToArray());
            var links = Links(sourceEditor).ToArray();
            Assert.AreEqual(2, links.Length);
            Assert.AreEqual(0, attempts.Count, "Rendering and editing must not launch saved links.");

            sourceWindow.ApplyAppearance(new NotesConfiguration { Theme = "dark" }, NoteColor.Pink);
            Pump(sourceWindow);
            Assert.AreEqual(((SolidColorBrush)sourceWindow.Resources["NotesLinkBrush"]).Color,
                ((SolidColorBrush)links[0].Foreground).Color);
            Assert.AreEqual(((SolidColorBrush)sourceWindow.Resources["AppSelectedBrush"]).Color,
                ((SolidColorBrush)sourceEditor.SelectionBrush).Color);
            links[0].RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Assert.AreEqual(((SolidColorBrush)sourceWindow.Resources["AppAccentBrush"]).Color,
                ((SolidColorBrush)links[0].Foreground).Color);
            links[0].RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            Assert.AreEqual(((SolidColorBrush)sourceWindow.Resources["NotesLinkBrush"]).Color,
                ((SolidColorBrush)links[0].Foreground).Color);

            Assert.IsTrue(links[0].Focus());
            Assert.AreEqual(((SolidColorBrush)sourceWindow.Resources["AppFocusBrush"]).Color,
                ((SolidColorBrush)links[0].Foreground).Color);
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(sourceEditor), 0, Key.Enter)
                { RoutedEvent = Keyboard.KeyDownEvent };
            links[0].RaiseEvent(key);
            Pump(sourceWindow);
            Assert.AreEqual(1, attempts.Count);
            Assert.AreEqual(firstDestination, attempts[0]);

            RaiseRequestNavigate(links[0], new Uri("javascript:alert(1)", UriKind.Absolute));
            Assert.AreEqual(1, attempts.Count, "Unsupported schemes must never reach the browser boundary.");

            RaiseRequestNavigate(links[1]);
            Assert.AreEqual(2, attempts.Count);
            Assert.AreEqual(failedDestination, attempts[1]);
            Assert.IsTrue(((TextBlock)sourceWindow.FindName("LinkStatus")).Text.Contains("could not be opened", StringComparison.Ordinal));
            Assert.AreEqual(document.GetPlainText(), app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)sourceWindow.FindName("SaveStatus")).Visibility);

            var paragraph = sourceEditor.Document.Blocks.OfType<Paragraph>().Single();
            sourceEditor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
            var copied = new DataObject(DataFormats.UnicodeText, string.Empty);
            sourceEditor.RaiseEvent(new DataObjectCopyingEventArgs(copied, false));
            Assert.AreEqual("Guide Unavailable", copied.GetData(DataFormats.UnicodeText));
            Assert.IsTrue(copied.GetDataPresent("Hood.Notes.Semantic.v1"));

            var targetEditor = (RichTextBox)targetWindow.FindName("Editor");
            targetEditor.SelectAll();
            var paste = new DataObjectPastingEventArgs(copied, false, DataFormats.UnicodeText);
            targetEditor.RaiseEvent(paste);
            Assert.IsTrue(paste.CommandCancelled);
            Pump(targetWindow);
            var pasted = targetApp.GetNote(targetId).Document;
            Assert.AreEqual("Guide Unavailable", pasted.GetPlainText());
            CollectionAssert.AreEquivalent(new[] { firstDestination, failedDestination },
                pasted.Paragraphs.SelectMany(row => row.Runs).Where(run => run.Kind == NoteInlineKind.Link)
                    .Select(run => run.LinkDestination).ToArray());

            RestoreHistory(sourceWindow, undo: true);
            Pump(sourceWindow);
            Assert.AreEqual(source, app.GetNote(noteId).Document.GetPlainText());
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link));
            RestoreHistory(sourceWindow, undo: false);
            Pump(sourceWindow);
            Assert.AreEqual("Guide Unavailable", app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual(2, app.GetNote(noteId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Count(run => run.Kind == NoteInlineKind.Link));
        }
        finally
        {
            sourceWindow.CloseWithoutMembershipChange();
            targetWindow.CloseWithoutMembershipChange();
        }
    }

    [TestMethod]
    public void TypedPlainUrlConvertsAtWhitespaceOrWhenTheCaretLeavesTheAddress()
    {
        const string url = "https://example.com/path?q=1#part";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, url);
            Pump(window);
            Assert.IsFalse(app.GetNote(noteId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link));

            Type(editor, " ");
            Pump(window);
            var completed = app.GetNote(noteId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.Link);
            Assert.AreEqual(url, completed.Text);
            Assert.AreEqual(url, completed.LinkDestination);
        }
        finally { window.CloseWithoutMembershipChange(); }

        var leaveApp = NotesApplication.Start(new MemoryNotesStore());
        var leaveId = leaveApp.CreateNote();
        var leaveWindow = CreateWindow(leaveApp, leaveId);
        try
        {
            ShowHidden(leaveWindow);
            var editor = (RichTextBox)leaveWindow.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, "open " + url);
            Pump(leaveWindow);
            Assert.IsFalse(leaveApp.GetNote(leaveId).Document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link));

            editor.CaretPosition = ((Paragraph)editor.Document.Blocks.FirstBlock).ContentStart;
            Pump(leaveWindow);
            Assert.AreEqual(url, leaveApp.GetNote(leaveId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.Link).LinkDestination);
        }
        finally { leaveWindow.CloseWithoutMembershipChange(); }

        var enterApp = NotesApplication.Start(new MemoryNotesStore());
        var enterId = enterApp.CreateNote();
        var enterWindow = CreateWindow(enterApp, enterId);
        try
        {
            ShowHidden(enterWindow);
            var editor = (RichTextBox)enterWindow.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Type(editor, "open " + url);
            Pump(enterWindow);
            PressEnter(editor, enterWindow);
            Assert.AreEqual(url, enterApp.GetNote(enterId).Document.Paragraphs[0].Runs
                .Single(run => run.Kind == NoteInlineKind.Link).LinkDestination);
        }
        finally { enterWindow.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void LinkContextMenuEditsCanCancelAndRemoveTheSemanticLink()
    {
        const string originalDestination = "https://example.com/start";
        var app = NotesApplication.Start(new MemoryNotesStore());
        var noteId = app.CreateNote();
        app.UpdateDocument(noteId, new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Runs = [new NoteTextRun("Label", NoteTextStyle.Bold, NoteInlineKind.Link, originalDestination)]
            }]
        });
        var window = CreateWindow(app, noteId);
        try
        {
            ShowHidden(window);
            var link = Links((RichTextBox)window.FindName("Editor")).Single();
            QueueDialogResult(window, "Edited", "https://edited.example/path", accepted: true);
            InvokeLinkMenu(link, "Edit link");
            Pump(window);

            var edited = app.GetNote(noteId).Document.Paragraphs[0].Runs.Single();
            Assert.AreEqual("Edited", edited.Text);
            Assert.AreEqual(NoteTextStyle.Bold, edited.Style);
            Assert.AreEqual(NoteInlineKind.Link, edited.Kind);
            Assert.AreEqual("https://edited.example/path", edited.LinkDestination);

            QueueDialogResult(window, "Discarded", "https://discarded.example", accepted: false);
            InvokeLinkMenu(Links((RichTextBox)window.FindName("Editor")).Single(), "Edit link");
            Pump(window);
            Assert.AreEqual("Edited", app.GetNote(noteId).Document.GetPlainText());
            Assert.AreEqual("https://edited.example/path", app.GetNote(noteId).Document.Paragraphs[0].Runs.Single().LinkDestination);

            InvokeLinkMenu(Links((RichTextBox)window.FindName("Editor")).Single(), "Remove link");
            var removed = app.GetNote(noteId).Document.Paragraphs[0].Runs.Single();
            Assert.AreEqual("Edited", removed.Text);
            Assert.AreEqual(NoteTextStyle.Bold, removed.Style);
            Assert.AreEqual(NoteInlineKind.Text, removed.Kind);
            Assert.IsNull(removed.LinkDestination);

            RestoreHistory(window, undo: true);
            Pump(window);
            var undone = app.GetNote(noteId).Document.Paragraphs[0].Runs.Single();
            Assert.AreEqual(NoteInlineKind.Link, undone.Kind);
            Assert.AreEqual("https://edited.example/path", undone.LinkDestination);
            RestoreHistory(window, undo: false);
            Pump(window);
            Assert.AreEqual(NoteInlineKind.Text, app.GetNote(noteId).Document.Paragraphs[0].Runs.Single().Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static NoteWindow CreateWindow(NotesApplication app, Guid noteId, Action<string>? openLink = null) => new(
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
        new NotesConfiguration(),
        openLink: openLink);

    private static IEnumerable<Hyperlink> Links(RichTextBox editor) =>
        editor.Document.Blocks.OfType<Paragraph>().SelectMany(paragraph => paragraph.Inlines.OfType<Hyperlink>());

    private static void RaiseRequestNavigate(Hyperlink link)
        => RaiseRequestNavigate(link, link.NavigateUri!);

    private static void RaiseRequestNavigate(Hyperlink link, Uri destination)
    {
        var args = new RequestNavigateEventArgs(destination, string.Empty)
            { RoutedEvent = Hyperlink.RequestNavigateEvent };
        link.RaiseEvent(args);
    }

    private static void InvokeLinkMenu(Hyperlink link, string header)
    {
        var item = link.ContextMenu!.Items.OfType<MenuItem>().Single(menuItem => (string)menuItem.Header == header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    private static void RestoreHistory(NoteWindow window, bool undo) =>
        typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [undo]);

    private static void QueueDialogResult(NoteWindow owner, string label, string destination, bool accepted)
    {
        owner.Dispatcher.BeginInvoke(new Action(() =>
        {
            var dialog = owner.OwnedWindows.OfType<Window>().Single(window => window.Title == "Edit link");
            var labelInput = (TextBox)dialog.GetType().GetField("_labelInput", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(dialog)!;
            var destinationInput = (TextBox)dialog.GetType().GetField("_destinationInput", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(dialog)!;
            labelInput.Text = label;
            destinationInput.Text = destination;
            dialog.DialogResult = accepted;
        }));
    }

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
