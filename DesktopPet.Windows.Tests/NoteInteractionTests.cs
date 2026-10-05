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
    [DataRow(NoteBlockKind.Paragraph, 240)]
    [DataRow(NoteBlockKind.Checklist, 240)]
    [DataRow(NoteBlockKind.Bulleted, 640)]
    [DataRow(NoteBlockKind.Numbered, 640)]
    public void WrappedTextLeavesAGapBeforeTheScrollbar(NoteBlockKind kind, int width)
    {
        var document = NotesDocument.FromText(new string('i', 2000)).SetListKind(0, 2000, kind);
        var window = CreateChecklistWindow(_ => { }, document: document);
        try
        {
            window.Width = width;
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var viewer = (ScrollViewer)editor.Template.FindName("PART_ContentHost", editor);
            var scrollbar = (ScrollBar)viewer.Template.FindName("PART_VerticalScrollBar", viewer);
            Assert.AreEqual(Visibility.Visible, viewer.ComputedVerticalScrollBarVisibility);
            AssertGap();
            editor.SelectAll();
            Click((ToggleButton)window.FindName("BoldButton"));
            AssertGap();

            void AssertGap()
            {
                editor.UpdateLayout();
                var scrollbarLeft = scrollbar.TranslatePoint(new Point(), editor).X;
                var rightmost = 0d;
                for (var position = editor.Document.ContentStart; position is not null;
                     position = position.GetNextInsertionPosition(LogicalDirection.Forward))
                {
                    var bounds = position.GetCharacterRect(LogicalDirection.Forward);
                    if (bounds.Top >= 0 && bounds.Bottom <= editor.ActualHeight)
                        rightmost = Math.Max(rightmost, bounds.Right);
                }
                Assert.IsTrue(scrollbarLeft - rightmost >= 10,
                    $"Wrapped text must leave a gap before the scrollbar; actual gap: {scrollbarLeft - rightmost:F1}.");
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    public void ScrollbarOnlyAppearsForScrollOrRightEdgeInteractionWithoutChangingLayout(string theme)
    {
        var window = CreateChecklistWindow(_ => { }, theme,
            NotesDocument.FromText(string.Join("\r\n", Enumerable.Repeat("Long note", 100))));
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var viewer = (ScrollViewer)editor.Template.FindName("PART_ContentHost", editor);
            var scrollbar = (ScrollBar)viewer.Template.FindName("PART_VerticalScrollBar", viewer);
            var viewportWidth = viewer.ViewportWidth;
            Assert.AreEqual(Visibility.Visible, scrollbar.Visibility);
            Assert.AreEqual(0d, scrollbar.Opacity, "An idle note must hide the scrollbar.");
            Assert.IsFalse(scrollbar.HasAnimatedProperties, "Initial content layout must not reveal the scrollbar.");
            Assert.IsTrue(scrollbar.IsHitTestVisible, "The invisible right edge must still receive pointer input.");

            foreach (var property in new[] { "IsMouseOver", "IsMouseCaptureWithin" })
            {
                // Simulate WPF input state without moving the user's desktop pointer.
                var key = (DependencyPropertyKey)typeof(UIElement)
                    .GetField(property + "PropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                scrollbar.SetValue(key, true);
                Assert.AreEqual(1d, scrollbar.Opacity, property + " must reveal the scrollbar.");
                scrollbar.SetValue(key, false);
                Assert.AreEqual(0d, scrollbar.Opacity, "Leaving the right edge must hide an idle scrollbar.");
            }

            viewer.ScrollToVerticalOffset(100);
            Pump(window);
            editor.UpdateLayout();
            Assert.IsTrue(viewer.VerticalOffset > 0, "The real note must scroll.");
            Assert.AreEqual(1d, scrollbar.Opacity, "Scrolling must reveal the scrollbar.");
            Assert.AreEqual(0d, scrollbar.GetAnimationBaseValue(UIElement.OpacityProperty),
                "The scrollbar must return to hidden when the scroll animation stops.");
            scrollbar.BeginAnimation(UIElement.OpacityProperty, null);
            editor.UpdateLayout();
            Assert.AreEqual(0d, scrollbar.Opacity);
            Assert.AreEqual(viewportWidth, viewer.ViewportWidth, "Revealing the scrollbar must not reflow the note.");
            Assert.IsTrue(viewer.VerticalOffset > 0, "Hiding the scrollbar must preserve the scroll position.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(12, 1)]
    [DataRow(16, 12)]
    [DataRow(24, 12)]
    [DataRow(32, 12)]
    public void ListMarkersStartAtTheBulletPosition(int textSize, int numberedItems)
    {
        var document = new NotesDocument
        {
            Paragraphs = new[] { NoteBlockKind.Bulleted }.Concat(Enumerable.Repeat(NoteBlockKind.Numbered, numberedItems))
                .Append(NoteBlockKind.Checklist).Select(kind => new NoteParagraph { Kind = kind, Runs = [new NoteTextRun("Task")] }).ToList()
        };
        var window = CreateChecklistWindow(_ => { }, document: document);
        try
        {
            window.Height = 1000;
            window.ApplyAppearance(new NotesConfiguration { Theme = "dark", TextSize = textSize }, NoteColor.Green);
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            var starts = editor.Document.Blocks.Cast<System.Windows.Documents.List>()
                .SelectMany(list => list.ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>())).Select(paragraph =>
            {
                var bounds = paragraph.Inlines.OfType<Run>().Single().ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var top = editor.TranslatePoint(bounds.TopLeft, content).Y;
                for (var x = 14; x < bitmap.PixelWidth - 14; x++)
                    for (var y = (int)top; y < Math.Min(bitmap.PixelHeight, (int)Math.Ceiling(top + bounds.Height)); y++)
                        if (pixels[y * stride + x * 4 + 3] > 32)
                            return x;
                Assert.Fail("The list row did not render.");
                return -1;
            }).ToArray();
            foreach (var start in starts.Skip(1))
                Assert.AreEqual(starts[0], start, 2, $"Bullet/number/checklist left edges: {string.Join(", ", starts)}.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NumberedMarkersRefreshAfterRowEditsAndStayOutOfSavedText(bool insert)
    {
        NotesDocument? saved = null;
        var document = NotesDocument.FromText("First\r\nTask").SetListKind(0, 11, NoteBlockKind.Numbered);
        var window = CreateChecklistWindow(result => saved = result, document: document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var list = (System.Windows.Documents.List)editor.Document.Blocks.FirstBlock;
            if (insert)
                list.ListItems.InsertBefore(list.ListItems.FirstListItem, new ListItem(new Paragraph(new Run("Inserted"))));
            else
                list.ListItems.Remove(list.ListItems.FirstListItem);

            list = (System.Windows.Documents.List)editor.Document.Blocks.FirstBlock;
            var paragraphs = list.ListItems.Select(item => (Paragraph)item.Blocks.FirstBlock).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, paragraphs.Length).Select(index => $"{index}.").ToArray(),
                paragraphs.Select(row => ((TextBlock)((InlineUIContainer)row.Inlines.FirstInline).Child).Text).ToArray());
            Assert.AreEqual(string.Empty, ((TextBlock)window.FindName("SaveStatus")).Text);
            var run = paragraphs.Last().Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart.GetPositionAtOffset(1)!, run.ContentStart.GetPositionAtOffset(3)!);
            Click((ToggleButton)window.FindName("BoldButton"));
            Assert.IsNotNull(saved);
            Assert.AreEqual(insert ? "Inserted\r\nFirst\r\nTask" : "Task", saved.GetPlainText());
            Assert.IsTrue(saved.Paragraphs.All(row => row.Kind == NoteBlockKind.Numbered));
            Assert.AreEqual("as", saved.Paragraphs.Last().Runs.Single(item => item.Style == NoteTextStyle.Bold).Text);
            Assert.AreEqual("as", editor.Selection.Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void EnterContinuesAndEndsNumberedLists()
    {
        NotesDocument? saved = null;
        var window = CreateChecklistWindow(result => saved = result,
            document: NotesDocument.FromText("Task").SetListKind(0, 4, NoteBlockKind.Numbered));
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var paragraph = (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock)
                .ListItems.FirstListItem.Blocks.FirstBlock;
            editor.CaretPosition = paragraph.Inlines.OfType<Run>().Single().ContentEnd;
            PressEnter();
            Assert.IsNotNull(saved);
            Assert.AreEqual(2, saved.Paragraphs.Count);
            Assert.IsTrue(saved.Paragraphs.All(row => row.Kind == NoteBlockKind.Numbered));
            var next = (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock)
                .ListItems.LastListItem.Blocks.FirstBlock;
            Assert.AreEqual("2.", ((TextBlock)((InlineUIContainer)next.Inlines.FirstInline).Child).Text);
            PressEnter();
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs.Last().Kind);
            Assert.AreEqual("Task\r\n", saved.GetPlainText());

            void PressEnter() => editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(editor), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        }
        finally { window.CloseWithoutMembershipChange(); }
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
            var paragraph = (Paragraph)((InlineUIContainer)checkbox.Parent).Parent;
            Assert.IsTrue(checkbox.IsEnabled, "Checklist checkbox cannot receive clicks.");
            Click(checkbox);
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.Paragraphs[0].IsChecked);
            Assert.AreEqual("Task", saved.GetPlainText());
            Assert.IsTrue(paragraph.TextDecorations.Any(item => item.Location == TextDecorationLocation.Strikethrough));
            Assert.IsTrue(saved.Paragraphs[0].Runs.All(run => run.Style == NoteTextStyle.None));
            Click(checkbox);
            Assert.IsFalse(saved.Paragraphs[0].IsChecked);
            Assert.AreEqual(0, paragraph.TextDecorations.Count);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CheckedChecklistRendersALineThroughItsText()
    {
        var window = CreateChecklistWindow(_ => { },
            document: NotesDocument.FromText("Task to do").SetListKind(0, 10, NoteBlockKind.Checklist));
        try
        {
            ShowHidden(window);
            var checkbox = ChecklistCheckbox(window);
            var paragraph = (Paragraph)((InlineUIContainer)checkbox.Parent).Parent;
            var run = paragraph.Inlines.OfType<Run>().Single();
            var editor = (RichTextBox)window.FindName("Editor");
            var content = (FrameworkElement)window.Content;
            var start = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var end = run.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
            var origin = editor.TranslatePoint(start.TopLeft, content);
            var crop = new Int32Rect((int)Math.Ceiling(origin.X) + 1, (int)Math.Ceiling(origin.Y),
                (int)(end.X - start.X) - 2, (int)start.Height);
            var before = RenderText();
            Click(checkbox);
            var after = RenderText();
            var muted = ((SolidColorBrush)window.FindResource("NotesSecondaryTextBrush")).Color;
            Assert.IsTrue(Enumerable.Range(0, after.Length / 4).Any(index =>
                after[index * 4] == muted.B && after[index * 4 + 1] == muted.G
                && after[index * 4 + 2] == muted.R && after[index * 4 + 3] == 255),
                "Completed text must actually render in the muted theme color.");
            Assert.IsTrue(Enumerable.Range(0, crop.Height).Any(y => Enumerable.Range(0, crop.Width)
                .Count(x => after[(y * crop.Width + x) * 4 + 3] - before[(y * crop.Width + x) * 4 + 3] > 32)
                    > crop.Width / 3), "Checking must paint a horizontal line through the text.");
            Click(checkbox);
            CollectionAssert.AreEqual(before, RenderText(), "Unchecking must restore the original rendered text.");

            byte[] RenderText()
            {
                Pump(window);
                var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var pixels = new byte[crop.Width * crop.Height * 4];
                bitmap.CopyPixels(crop, pixels, crop.Width * 4, 0);
                return pixels;
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("dark", false)]
    [DataRow("light", false)]
    [DataRow("dark", true)]
    [DataRow("light", true)]
    public void CheckedChecklistTextUsesMutedColorAndRestoresNormalColor(string theme, bool initiallyChecked)
    {
        NotesDocument? saved = null;
        var document = NotesDocument.FromText("Task").SetListKind(0, 4, NoteBlockKind.Checklist)
            .SetStyle(0, 4, NoteTextStyle.Bold, true).SetChecklistChecked(0, initiallyChecked);
        var window = CreateChecklistWindow(result => saved = result, theme, document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            if (!initiallyChecked)
            {
                AssertTextColor(editor.Foreground);
                Click(ChecklistCheckbox(window));
            }
            AssertTextColor((Brush)window.FindResource("NotesSecondaryTextBrush"));
            window.ApplyAppearance(new NotesConfiguration { Theme = theme == "dark" ? "light" : "dark" }, NoteColor.Green);
            AssertTextColor((Brush)window.FindResource("NotesSecondaryTextBrush"));
            editor.SelectAll();
            Click((ToggleButton)window.FindName("ItalicButton"));
            AssertTextColor((Brush)window.FindResource("NotesSecondaryTextBrush"));
            Click(ChecklistCheckbox(window));
            AssertTextColor(editor.Foreground);
            Assert.IsNotNull(saved);
            Assert.IsFalse(saved.Paragraphs[0].IsChecked);
            Assert.AreEqual("Task", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic, saved.Paragraphs[0].Runs.Single().Style);

            void AssertTextColor(Brush expected)
            {
                var paragraph = (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock)
                    .ListItems.FirstListItem.Blocks.FirstBlock;
                Assert.AreEqual(((SolidColorBrush)expected).Color,
                    ((SolidColorBrush)paragraph.Inlines.OfType<Run>().Single().Foreground).Color);
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CheckedChecklistRestoresCompletionAndPreservesManualFormattingDuringEdits()
    {
        NotesDocument? saved = null;
        var document = new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Kind = NoteBlockKind.Checklist, IsChecked = true,
                Runs = [new NoteTextRun("Task", NoteTextStyle.Underline),
                    new NoteTextRun(" done", NoteTextStyle.Strikethrough)]
            }]
        };
        var window = CreateChecklistWindow(result => saved = result, document: document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var checkbox = ChecklistCheckbox(window);
            var paragraph = (Paragraph)((InlineUIContainer)checkbox.Parent).Parent;
            Assert.IsTrue(paragraph.TextDecorations.Any(item => item.Location == TextDecorationLocation.Strikethrough));
            paragraph.Inlines.Add(new Run(" again"));
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.Paragraphs[0].IsChecked);
            Assert.AreEqual("Task done again", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Underline, saved.Paragraphs[0].Runs[0].Style);
            Assert.AreEqual(NoteTextStyle.Strikethrough, saved.Paragraphs[0].Runs[1].Style);
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[0].Runs[2].Style);

            editor.SelectAll();
            Click((ToggleButton)window.FindName("BoldButton"));
            checkbox = ChecklistCheckbox(window);
            paragraph = (Paragraph)((InlineUIContainer)checkbox.Parent).Parent;
            Assert.IsTrue(paragraph.TextDecorations.Any(item => item.Location == TextDecorationLocation.Strikethrough));
            Click(checkbox);
            Assert.AreEqual(0, paragraph.TextDecorations.Count);
            Assert.IsFalse(saved.Paragraphs[0].IsChecked);
            CollectionAssert.AreEqual(new[] { NoteTextStyle.Bold | NoteTextStyle.Underline,
                NoteTextStyle.Bold | NoteTextStyle.Strikethrough, NoteTextStyle.Bold },
                saved.Paragraphs[0].Runs.Select(run => run.Style).ToArray());
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
            var colors = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Note _color");
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
