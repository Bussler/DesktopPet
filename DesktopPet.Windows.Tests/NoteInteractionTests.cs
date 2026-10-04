using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteInteractionTests
{
    [TestMethod]
    [DataRow("dark", NoteColor.Pink)]
    [DataRow("light", NoteColor.Yellow)]
    public void EditorKeepsNoteColorWhenFocusedAndUsingMenus(string theme, NoteColor color)
    {
        var window = CreateChecklistWindow(_ => { }, theme);
        var editor = (RichTextBox)window.FindName("Editor");
        var menu = ((Button)window.FindName("MenuButton")).ContextMenu!;
        try
        {
            window.ApplyAppearance(new NotesConfiguration { Theme = theme }, color);
            ShowHidden(window);
            Assert.IsTrue(editor.Focus(), "The editor must have real keyboard focus for this regression.");
            Pump(window);
            AssertEditorColor();
            OpenMenu(menu);
            AssertEditorColor();
            menu.IsOpen = false;
            editor.Focus();
            Pump(window);
            AssertEditorColor();
        }
        finally { menu.IsOpen = false; window.CloseWithoutMembershipChange(); }

        void AssertEditorColor()
        {
            var content = (FrameworkElement)window.Content;
            var point = editor.TranslatePoint(new Point(editor.ActualWidth / 2, editor.ActualHeight / 2), content);
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen())
                drawing.DrawRectangle(window.Background, null, new Rect(content.RenderSize));
            bitmap.Render(background);
            bitmap.Render(content);
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect((int)point.X, (int)point.Y, 1, 1), pixel, 4, 0);
            Assert.AreEqual(((SolidColorBrush)window.Background).Color,
                Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]), "The editor painted over the selected note color.");
        }
    }

    [TestMethod]
    public void ChecklistUsesHandCursorBeforeAndAfterClicking()
    {
        var window = CreateChecklistWindow(_ => { });
        try
        {
            var checkbox = ChecklistCheckbox(window);
            Assert.AreSame(Cursors.Hand, checkbox.Cursor);
            var query = new QueryCursorEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.QueryCursorEvent };
            checkbox.RaiseEvent(query);
            Assert.AreSame(Cursors.Hand, query.Cursor);
            Click(checkbox);
            Assert.AreSame(Cursors.Hand, checkbox.Cursor);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ChecklistCheckboxIsSmallAndClickable()
    {
        var window = CreateChecklistWindow(_ => { });
        try
        {
            var checkbox = ChecklistCheckbox(window);
            checkbox.Measure(new Size(300, 100));
            Assert.IsTrue(checkbox.IsEnabled, "Checklist checkbox is disabled.");
            Assert.IsTrue(checkbox.DesiredSize.Width <= 24 && checkbox.DesiredSize.Height <= 24,
                $"Checklist checkbox is too large: {checkbox.DesiredSize}.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ClickingChecklistPersistsCheckedStateAndPreservesText()
    {
        NotesDocument? saved = null;
        var window = CreateChecklistWindow(document => saved = document);
        try
        {
            var checkbox = ChecklistCheckbox(window);
            Assert.IsTrue(checkbox.IsEnabled, "Checklist checkbox cannot receive clicks.");
            Click(checkbox);
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.Paragraphs[0].IsChecked);
            Assert.AreEqual("Task", saved.GetPlainText());
            Click(checkbox);
            Assert.IsFalse(saved.Paragraphs[0].IsChecked);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ChecklistClickUsesCurrentRowAfterParagraphInsertionOrRemoval(bool insert)
    {
        NotesDocument? saved = null;
        var document = NotesDocument.FromText("Heading\r\nTask").SetListKind(9, 4, NoteBlockKind.Checklist);
        var window = CreateChecklistWindow(result => saved = result, document: document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var list = (System.Windows.Documents.List)editor.Document.Blocks.LastBlock;
            var checkbox = (CheckBox)((Paragraph)list.ListItems.FirstListItem.Blocks.FirstBlock)
                .Inlines.OfType<InlineUIContainer>().Single().Child;
            if (insert)
                editor.Document.Blocks.InsertBefore(list, new Paragraph(new Run("Inserted")));
            else
                editor.Document.Blocks.Remove(editor.Document.Blocks.FirstBlock);

            Click(checkbox);
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.Paragraphs.Last().IsChecked);
            Assert.AreEqual("Task", string.Concat(saved.Paragraphs.Last().Runs.Select(run => run.Text)));
            Assert.IsTrue(saved.Paragraphs.Take(saved.Paragraphs.Count - 1).All(row => !row.IsChecked));
            Assert.AreEqual(string.Empty, ((TextBlock)window.FindName("SaveStatus")).Text);
            Click(checkbox);
            Assert.IsFalse(saved.Paragraphs.Last().IsChecked);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("BoldButton", NoteTextStyle.Bold)]
    [DataRow("ItalicButton", NoteTextStyle.Italic)]
    [DataRow("UnderlineButton", NoteTextStyle.Underline)]
    [DataRow("StrikethroughButton", NoteTextStyle.Strikethrough)]
    public void FormattingSelectedChecklistTextWorks(string buttonName, NoteTextStyle style)
    {
        NotesDocument? saved = null;
        var window = CreateChecklistWindow(document => saved = document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            editor.SelectAll();
            var button = (ToggleButton)window.FindName(buttonName);
            Assert.IsTrue(button.IsEnabled);
            Click(button);
            Assert.IsNotNull(saved, "The formatting click did not save an edit.");
            Assert.AreEqual("Task", saved.GetPlainText());
            Assert.IsTrue(saved.Paragraphs[0].Runs.All(run => run.Style.HasFlag(style)),
                $"{buttonName} did not format the checklist text.");
            Assert.AreEqual(NoteBlockKind.Checklist, saved.Paragraphs[0].Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Checklist)]
    [DataRow(NoteBlockKind.Bulleted)]
    [DataRow(NoteBlockKind.Numbered)]
    public void FormattingTextAfterListDoesNotShiftTheSelection(NoteBlockKind kind)
    {
        NotesDocument? saved = null;
        var document = NotesDocument.FromText("Task\r\nNote").SetListKind(0, 4, kind);
        var window = CreateChecklistWindow(result => saved = result, document: document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var run = ((Paragraph)editor.Document.Blocks.LastBlock).Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart, run.ContentEnd);
            Click((ToggleButton)window.FindName("BoldButton"));
            Assert.IsNotNull(saved);
            Assert.AreEqual("Task\r\nNote", saved.GetPlainText());
            Assert.IsTrue(saved.Paragraphs[0].Runs.All(item => item.Style == NoteTextStyle.None));
            Assert.IsTrue(saved.Paragraphs[1].Runs.All(item => item.Style.HasFlag(NoteTextStyle.Bold)));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void PartialChecklistSelectionCanApplyAndRemoveFormatting()
    {
        NotesDocument? saved = null;
        var window = CreateChecklistWindow(document => saved = document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var paragraph = (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock)
                .ListItems.FirstListItem.Blocks.FirstBlock;
            var run = paragraph.Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart.GetPositionAtOffset(1)!, run.ContentStart.GetPositionAtOffset(3)!);
            var button = (ToggleButton)window.FindName("BoldButton");
            Click(button);
            Assert.IsNotNull(saved);
            CollectionAssert.AreEqual(new[] { "T", "as", "k" }, saved.Paragraphs[0].Runs.Select(item => item.Text).ToArray());
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[0].Runs[0].Style);
            Assert.AreEqual(NoteTextStyle.Bold, saved.Paragraphs[0].Runs[1].Style);
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[0].Runs[2].Style);
            Click(button);
            Assert.AreEqual("Task", saved.GetPlainText());
            Assert.IsTrue(saved.Paragraphs[0].Runs.All(item => item.Style == NoteTextStyle.None));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("\r")]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void FormattingAfterSoftLineBreakKeepsTextAndSelection(string lineBreak)
    {
        NotesDocument? saved = null;
        var document = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Runs = [new NoteTextRun("Head" + lineBreak + "Tail")] }]
        };
        var window = CreateChecklistWindow(result => saved = result, document: document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var tail = ((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Last();
            editor.Selection.Select(tail.ContentStart, tail.ContentEnd);
            Click((ToggleButton)window.FindName("BoldButton"));

            Assert.IsNotNull(saved);
            Assert.AreEqual("Head\r\nTail", saved.GetPlainText());
            Assert.AreEqual("Tail", saved.Paragraphs[0].Runs.Last().Text);
            Assert.AreEqual(NoteTextStyle.Bold, saved.Paragraphs[0].Runs.Last().Style);
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[0].Runs.First().Style);
            Assert.AreEqual("Tail", editor.Selection.Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ChecklistFormattingPreservesUserTypedTabs()
    {
        NotesDocument? saved = null;
        var document = NotesDocument.FromText("T\task").ToggleListKind(0, 5, NoteBlockKind.Checklist);
        var window = CreateChecklistWindow(result => saved = result, document: document);
        try
        {
            ((RichTextBox)window.FindName("Editor")).SelectAll();
            Click((ToggleButton)window.FindName("ItalicButton"));
            Assert.IsNotNull(saved);
            Assert.AreEqual("T\task", saved.GetPlainText());
            Assert.IsTrue(saved.Paragraphs[0].Runs.All(run => run.Style.HasFlag(NoteTextStyle.Italic)));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("sticky-notes", "light", 217)]
    [DataRow("sticky-notes", "dark", 217)]
    [DataRow("minimal", "light", 255)]
    [DataRow("minimal", "dark", 255)]
    [DataRow("soft", "light", 255)]
    [DataRow("soft", "dark", 255)]
    [DataRow("studio", "light", 255)]
    [DataRow("studio", "dark", 255)]
    public void MenuBackgroundHasRequestedOpacityAndReceivesHits(string style, string theme, int expectedAlpha)
    {
        var window = CreateChecklistWindow(_ => { }, theme, uiStyle: style);
        var menu = ((Button)window.FindName("MenuButton")).ContextMenu!;
        try
        {
            OpenMenu(menu);
            Assert.IsInstanceOfType<SolidColorBrush>(menu.Background);
            Assert.AreEqual((byte)expectedAlpha, ((SolidColorBrush)menu.Background).Color.A,
                "The shared menu surface must retain its style opacity and fully opaque text.");
            Assert.IsNotNull(menu.InputHitTest(new Point(menu.ActualWidth / 2, 2)),
                "Blank menu padding must receive hits instead of exposing the window behind it.");
            foreach (MenuItem item in menu.Items)
                Assert.IsNotNull(menu.InputHitTest(item.TranslatePoint(new Point(8, item.ActualHeight / 2), menu)),
                    "Menu choices must receive hits as the pointer moves over them.");
        }
        finally { menu.IsOpen = false; window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("sticky-notes", "light", 217)]
    [DataRow("sticky-notes", "dark", 217)]
    [DataRow("minimal", "light", 255)]
    [DataRow("minimal", "dark", 255)]
    [DataRow("soft", "light", 255)]
    [DataRow("soft", "dark", 255)]
    [DataRow("studio", "light", 255)]
    [DataRow("studio", "dark", 255)]
    public void ColorSubmenuHasClickableBackgroundAndExplicitCheckboxes(string style, string theme, int expectedAlpha)
    {
        var window = CreateChecklistWindow(_ => { }, theme, uiStyle: style);
        var menu = ((Button)window.FindName("MenuButton")).ContextMenu!;
        try
        {
            OpenMenu(menu);
            var colors = (MenuItem)menu.Items[1];
            colors.IsSubmenuOpen = true;
            menu.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var border = (Border)colors.Template.FindName("SubmenuBorder", colors);
            Assert.IsInstanceOfType<SolidColorBrush>(border.Background);
            Assert.AreEqual((byte)expectedAlpha, ((SolidColorBrush)border.Background).Color.A);
            Assert.IsNotNull(border.InputHitTest(new Point(border.ActualWidth / 2, 2)));
            foreach (MenuItem item in colors.Items)
            {
                var checkbox = (Border)item.Template.FindName("CheckBoxIconBorder", item);
                Assert.IsNotNull(checkbox, "A color choice must draw a checkbox outline.");
                Assert.AreEqual(Visibility.Visible, checkbox.Visibility);
                Assert.IsInstanceOfType<SolidColorBrush>(checkbox.BorderBrush);
                Assert.IsTrue(((SolidColorBrush)checkbox.BorderBrush).Color.A > 0);
            }
        }
        finally { menu.IsOpen = false; window.CloseWithoutMembershipChange(); }
    }

    private static void OpenMenu(System.Windows.Controls.ContextMenu menu)
    {
        // Initialize the real popup without displaying it or sending desktop mouse input.
        menu.Opacity = 0;
        menu.Placement = PlacementMode.Absolute;
        menu.HorizontalOffset = menu.VerticalOffset = -10000;
        menu.IsOpen = true;
        menu.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        menu.UpdateLayout();
    }

    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.ShowInTaskbar = false;
        window.Left = window.Top = -10000;
        window.Show();
        window.Activate();
        Pump(window);
    }

    private static void Pump(Window window) => window.Dispatcher.Invoke(() => { },
        System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private static NoteWindow CreateChecklistWindow(
        Action<NotesDocument> save, string theme = "dark", NotesDocument? document = null,
        string uiStyle = UiStyleCatalog.StickyNotes) => new(
        new HoodNote { Document = document ?? NotesDocument.FromText("Task").ToggleListKind(0, 4, NoteBlockKind.Checklist) },
        (_, document) => { save(document.Copy()); return null; }, (_, _) => null, (_, _) => null,
        (_, _) => (null, null), _ => (true, null), () => { }, () => { }, () => { },
        (_, _) => null, _ => null, () => null, new NotesConfiguration { Theme = theme }, uiStyle);

    private static CheckBox ChecklistCheckbox(NoteWindow window) => (CheckBox)((Paragraph)
        ((System.Windows.Documents.List)((RichTextBox)window.FindName("Editor")).Document.Blocks.FirstBlock)
        .ListItems.FirstListItem.Blocks.FirstBlock).Inlines.OfType<InlineUIContainer>().Single().Child;

    // Invoke WPF's actual click/toggle path without sending mouse input to the desktop.
    private static void Click(ToggleButton button) => typeof(ToggleButton)
        .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
}
