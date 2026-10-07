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
