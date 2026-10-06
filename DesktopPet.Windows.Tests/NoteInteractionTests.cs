using System.Reflection;
using System.IO;
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
    public void NewNoteButtonIsInTheHeaderAndInvokesCreation()
    {
        var created = 0;
        var window = new NoteWindow(new HoodNote(), (_, _) => null, (_, _) => null, (_, _) => null,
            (_, _) => (null, null), _ => (true, null), () => created++, () => { }, () => { },
            (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            var button = (Button)window.FindName("NewNoteButton");
            Assert.AreEqual(window.FindName("NoteHeader"), ((DockPanel)button.Parent).Parent);
            Assert.AreEqual("New note", System.Windows.Automation.AutomationProperties.GetName(button));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, created);
            var menu = ((Button)window.FindName("MenuButton")).ContextMenu!;
            CollectionAssert.AreEqual(new[] { "Note _color", "_Notes list", "_Delete note", "Notes _settings" },
                menu.Items.OfType<MenuItem>().Select(item => (string)item.Header).ToArray());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Paragraph, false)]
    [DataRow(NoteBlockKind.Bulleted, false)]
    [DataRow(NoteBlockKind.Numbered, false)]
    [DataRow(NoteBlockKind.Checklist, false)]
    [DataRow(NoteBlockKind.Code, false)]
    [DataRow(NoteBlockKind.Paragraph, true)]
    public void LineSpacingChangesRenderedLinesWithoutEditingContent(NoteBlockKind kind, bool tableCell)
    {
        var saved = 0;
        var document = new NotesDocument
        {
            Paragraphs = [new() { Kind = kind, Runs = [new NoteTextRun(string.Concat(Enumerable.Repeat("word ", 100)))],
                Cell = tableCell ? new NoteTableCell(Guid.NewGuid(), 0, 0) : null }]
        };
        var window = CreateChecklistWindow(_ => saved++, document: document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var flow = editor.Document;
            var start = FirstParagraph().ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            editor.Selection.Select(start, start.GetPositionAtOffset(4)!);
            var selection = editor.Selection.Text;
            var normalSpacing = MeasureSpacing();
            Assert.IsTrue(double.IsNaN(flow.LineHeight), "The default must retain automatic font spacing.");
            window.ApplyAppearance(new NotesConfiguration { Theme = "dark", LineSpacing = 1 }, NoteColor.Yellow);
            Assert.IsTrue(MeasureSpacing() < normalSpacing, "Values below 2 must bring rendered lines closer together.");
            Assert.AreEqual(LineStackingStrategy.BlockLineHeight, flow.LineStackingStrategy);
            Assert.AreEqual(4.0, FirstParagraph().Margin.Bottom);
            window.ApplyAppearance(new NotesConfiguration { Theme = "dark", LineSpacing = 3 }, NoteColor.Yellow);
            Assert.IsTrue(MeasureSpacing() > normalSpacing, "Values above 2 must move rendered lines farther apart.");
            window.ApplyAppearance(new NotesConfiguration { Theme = "dark", LineSpacing = 1, TextSize = 24 }, NoteColor.Yellow);
            Assert.AreEqual((editor.FontFamily.LineSpacing - 0.25) * 24, flow.LineHeight, 0.001);
            Assert.AreSame(flow, editor.Document);
            Assert.AreEqual(selection, editor.Selection.Text);
            typeof(NoteWindow).GetMethod("SetEditorDocument", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [document, 0, 4]);
            Assert.AreEqual(flow.LineHeight, editor.Document.LineHeight, "Document rebuilds must keep the chosen spacing.");
            Assert.AreEqual(flow.LineStackingStrategy, editor.Document.LineStackingStrategy);
            Assert.AreEqual(4.0, FirstParagraph().Margin.Bottom);
            window.ApplyAppearance(new NotesConfiguration { Theme = "dark" }, NoteColor.Yellow);
            Assert.AreEqual(normalSpacing, MeasureSpacing(), 0.1);
            Assert.AreEqual(LineStackingStrategy.MaxHeight, editor.Document.LineStackingStrategy);
            Assert.AreEqual(8.0, FirstParagraph().Margin.Bottom);
            Assert.AreEqual(0, saved, "Appearance changes must not save content edits.");

            double MeasureSpacing()
            {
                editor.UpdateLayout();
                var position = FirstParagraph().ContentStart.GetInsertionPosition(LogicalDirection.Forward);
                var nextLine = position.GetLineStartPosition(1);
                Assert.IsNotNull(nextLine, "The sample must wrap into multiple lines.");
                return nextLine.GetCharacterRect(LogicalDirection.Forward).Top - position.GetCharacterRect(LogicalDirection.Forward).Top;
            }

            Paragraph FirstParagraph() => editor.Document.Blocks.FirstBlock switch
            {
                Paragraph paragraph => paragraph,
                System.Windows.Documents.List list => (Paragraph)list.ListItems.FirstListItem.Blocks.FirstBlock,
                Table table => (Paragraph)table.RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock,
                _ => throw new InvalidOperationException("Expected text in the sample document.")
            };
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(12)]
    [DataRow(16)]
    [DataRow(32)]
    public void CompactSpacingAlsoBringsSeparateParagraphsCloserTogether(int size)
    {
        var window = CreateChecklistWindow(_ => { }, document: NotesDocument.FromText("First\r\nSecond"));
        try
        {
            window.ApplyAppearance(new NotesConfiguration { TextSize = size }, NoteColor.Yellow);
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var normal = MeasureParagraphDistance();
            window.ApplyAppearance(new NotesConfiguration { TextSize = size, LineSpacing = 1 }, NoteColor.Yellow);
            Assert.IsTrue(MeasureParagraphDistance() < normal - 4, "Compact spacing must reduce both line height and paragraph spacing.");

            double MeasureParagraphDistance()
            {
                editor.UpdateLayout();
                var paragraphs = editor.Document.Blocks.OfType<Paragraph>().ToArray();
                return paragraphs[1].ContentStart.GetCharacterRect(LogicalDirection.Forward).Top
                    - paragraphs[0].ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CompactSpacingPreservesWideNumberedMarkerIndentation()
    {
        var text = string.Join("\r\n", Enumerable.Repeat("Numbered item", 20));
        var window = CreateChecklistWindow(_ => { }, document: NotesDocument.FromText(text).SetListKind(0, text.Length, NoteBlockKind.Numbered));
        try
        {
            window.ApplyAppearance(new NotesConfiguration { TextSize = 32 }, NoteColor.Yellow);
            var editor = (RichTextBox)window.FindName("Editor");
            var paragraph = (Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock).ListItems.FirstListItem.Blocks.FirstBlock;
            var leftMargin = paragraph.Margin.Left;
            var indent = paragraph.TextIndent;
            Assert.IsTrue(leftMargin > 0, "The sample needs enough space for a two-digit marker.");
            window.ApplyAppearance(new NotesConfiguration { TextSize = 32, LineSpacing = 1 }, NoteColor.Yellow);
            Assert.AreEqual(leftMargin, paragraph.Margin.Left);
            Assert.AreEqual(indent, paragraph.TextIndent);
            Assert.AreEqual(4.0, paragraph.Margin.Bottom);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

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
    public void ListMarkersStartAtTheNormalTextPosition(int textSize, int numberedItems)
    {
        var document = new NotesDocument
        {
            Paragraphs = new[] { NoteBlockKind.Paragraph, NoteBlockKind.Bulleted }.Concat(Enumerable.Repeat(NoteBlockKind.Numbered, numberedItems))
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
            var starts = editor.Document.Blocks.SelectMany(block => block is Paragraph paragraph
                ? new[] { paragraph }
                : ((System.Windows.Documents.List)block).ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>())).Select(paragraph =>
            {
                var bounds = paragraph.Inlines.OfType<Run>().Single().ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var top = editor.TranslatePoint(bounds.TopLeft, content).Y;
                for (var x = 12; x < bitmap.PixelWidth - 14; x++)
                    for (var y = (int)top; y < Math.Min(bitmap.PixelHeight, (int)Math.Ceiling(top + bounds.Height)); y++)
                        if (pixels[y * stride + x * 4 + 3] > 32)
                            return x;
                Assert.Fail("The list row did not render.");
                return -1;
            }).ToArray();
            foreach (var start in starts.Skip(1))
                Assert.AreEqual(starts[0], start, 2, $"Text/bullet/number/checklist left edges: {string.Join(", ", starts)}.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted, false)]
    [DataRow(NoteBlockKind.Bulleted, true)]
    [DataRow(NoteBlockKind.Bulleted, true, true)]
    [DataRow(NoteBlockKind.Numbered, false)]
    [DataRow(NoteBlockKind.Checklist, false)]
    public void RichTextTransferBetweenNotesPreservesListMarkers(NoteBlockKind kind, bool selectTextOnly, bool singleItem = false)
    {
        var text = singleItem ? "First" : "First\r\nSecond";
        var document = NotesDocument.FromText(text).SetListKind(0, text.Length, kind).SetStyle(0, 5, NoteTextStyle.Bold, true);
        if (kind == NoteBlockKind.Checklist) document = document.SetChecklistChecked(0, true);
        NotesDocument? saved = null;
        var source = CreateChecklistWindow(_ => { }, document: document);
        var target = CreateChecklistWindow(result => saved = result, document: NotesDocument.FromText(""));
        try
        {
            var sourceEditor = (RichTextBox)source.FindName("Editor");
            var targetEditor = (RichTextBox)target.FindName("Editor");
            using var payload = new MemoryStream();
            var listItems = ((System.Windows.Documents.List)sourceEditor.Document.Blocks.FirstBlock).ListItems;
            var range = selectTextOnly
                ? new TextRange(((Paragraph)listItems.FirstListItem.Blocks.FirstBlock).Inlines.OfType<Run>().First().ContentStart,
                    ((Paragraph)listItems.LastListItem.Blocks.FirstBlock).Inlines.OfType<Run>().Last().ContentEnd)
                : new TextRange(sourceEditor.Document.ContentStart, sourceEditor.Document.ContentEnd);
            sourceEditor.Selection.Select(range.Start, range.End);
            range.Save(payload, DataFormats.Xaml);
            var data = new DataObject(DataFormats.Xaml, System.Text.Encoding.UTF8.GetString(payload.ToArray()));
            sourceEditor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            payload.Position = 0;
            targetEditor.SelectAll();
            var paste = new DataObjectPastingEventArgs(data, false, DataFormats.Xaml);
            targetEditor.RaiseEvent(paste);
            if (!paste.CommandCancelled) targetEditor.Selection.Load(payload, DataFormats.Xaml);
            Pump(target);
            Assert.IsNotNull(saved);
            Assert.AreEqual(document.GetPlainText(), saved.GetPlainText());
            Assert.IsTrue(saved.Paragraphs.All(row => row.Kind == kind));
            Assert.IsTrue(saved.Paragraphs[0].Runs.All(run => run.Style.HasFlag(NoteTextStyle.Bold)));
            var list = (System.Windows.Documents.List)targetEditor.Document.Blocks.FirstBlock;
            if (kind == NoteBlockKind.Bulleted)
                Assert.AreEqual(TextMarkerStyle.Disc, list.MarkerStyle);
            else
                Assert.IsTrue(list.ListItems.All(item => ((Paragraph)item.Blocks.FirstBlock).Inlines
                    .OfType<InlineUIContainer>().Any()), "Pasted list markers must remain visible and editable.");
            Assert.AreEqual(string.Empty, ((TextBlock)target.FindName("SaveStatus")).Text);
            if (kind == NoteBlockKind.Checklist)
            {
                var checkbox = ChecklistCheckbox(target);
                Assert.IsTrue(checkbox.IsChecked);
                Click(checkbox);
                Assert.IsFalse(saved.Paragraphs[0].IsChecked);
                Assert.AreEqual(text, saved.GetPlainText());
            }
        }
        finally { source.CloseWithoutMembershipChange(); target.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ListPasteReplacesOnlySelectedTextAndPreservesSurroundingContent(bool insideTable)
    {
        var sourceDocument = NotesDocument.FromText("First\r\nSecond").SetListKind(0, 13, NoteBlockKind.Bulleted)
            .SetStyle(0, 5, NoteTextStyle.Bold, true);
        var targetDocument = NotesDocument.FromText("Before\r\nleft OLD right\r\nAfter");
        if (insideTable) targetDocument.Paragraphs[1].Cell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        NotesDocument? saved = null;
        var source = CreateChecklistWindow(_ => { }, document: sourceDocument);
        var target = CreateChecklistWindow(result => saved = result, document: targetDocument);
        try
        {
            var editor = (RichTextBox)target.FindName("Editor");
            var paragraph = insideTable
                ? (Paragraph)((Table)editor.Document.Blocks.ElementAt(1)).RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock
                : (Paragraph)editor.Document.Blocks.ElementAt(1);
            var run = paragraph.Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart.GetPositionAtOffset(5)!, run.ContentStart.GetPositionAtOffset(8)!);
            var sourceEditor = (RichTextBox)source.FindName("Editor");
            sourceEditor.SelectAll();
            var data = new DataObject(DataFormats.UnicodeText, sourceDocument.GetPlainText());
            sourceEditor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
            editor.RaiseEvent(new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText));
            Pump(target);
            Assert.IsNotNull(saved);
            Assert.AreEqual("Before\r\nleft First\r\nSecond right\r\nAfter", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Bulleted, saved.Paragraphs[1].Kind);
            Assert.AreEqual(NoteBlockKind.Bulleted, saved.Paragraphs[2].Kind);
            Assert.AreEqual(targetDocument.Paragraphs[1].Cell, saved.Paragraphs[1].Cell);
            Assert.AreEqual(targetDocument.Paragraphs[1].Cell, saved.Paragraphs[2].Cell);
            Assert.AreEqual(NoteTextStyle.Bold, saved.Paragraphs[1].Runs.Single(item => item.Text == "First").Style);
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[1].Runs.Single(item => item.Text == "left ").Style);
            Assert.IsTrue(editor.Selection.IsEmpty);
            Assert.AreEqual(" right", new TextRange(editor.CaretPosition, editor.CaretPosition.Paragraph!.ContentEnd).Text);
            NotesSnapshotValidator.ValidateDocument(saved);
        }
        finally { source.CloseWithoutMembershipChange(); target.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted, false)]
    [DataRow(NoteBlockKind.Numbered, false)]
    [DataRow(NoteBlockKind.Checklist, false)]
    [DataRow(NoteBlockKind.Bulleted, true)]
    [DataRow(NoteBlockKind.Numbered, true)]
    [DataRow(NoteBlockKind.Checklist, true)]
    public void ListCopiedFromTableCellPreservesMarkersAndUsesDestinationCell(NoteBlockKind kind, bool targetInTable)
    {
        var document = NotesDocument.FromText("First\r\nOther").SetListKind(0, 5, kind)
            .SetStyle(0, 5, NoteTextStyle.Bold, true);
        var sourceCell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        foreach (var row in document.Paragraphs) row.Cell = sourceCell;
        if (kind == NoteBlockKind.Checklist) document = document.SetChecklistChecked(0, true);
        var destination = NotesDocument.FromText("old");
        if (targetInTable) destination.Paragraphs[0].Cell = new NoteTableCell(Guid.NewGuid(), 0, 0);
        NotesDocument? saved = null;
        var source = CreateChecklistWindow(_ => { }, document: document);
        var target = CreateChecklistWindow(result => saved = result, document: destination);
        try
        {
            var editor = (RichTextBox)source.FindName("Editor");
            var cell = ((Table)editor.Document.Blocks.FirstBlock).RowGroups[0].Rows[0].Cells[0];
            var paragraph = (Paragraph)((System.Windows.Documents.List)cell.Blocks.FirstBlock).ListItems.FirstListItem.Blocks.FirstBlock;
            var run = paragraph.Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart, run.ContentEnd);
            var data = CopySelection(editor);
            var targetEditor = (RichTextBox)target.FindName("Editor");
            targetEditor.SelectAll();
            PasteSelection(targetEditor, data);
            Assert.IsNotNull(saved);
            Assert.AreEqual("First", saved.GetPlainText());
            Assert.AreEqual(kind, saved.Paragraphs.Single().Kind);
            Assert.AreEqual(kind == NoteBlockKind.Checklist, saved.Paragraphs[0].IsChecked);
            Assert.AreEqual(destination.Paragraphs[0].Cell, saved.Paragraphs[0].Cell);
            Assert.IsTrue(saved.Paragraphs[0].Runs.All(item => item.Style == NoteTextStyle.Bold));
            NotesSnapshotValidator.ValidateDocument(saved);
        }
        finally { source.CloseWithoutMembershipChange(); target.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(NoteBlockKind.Bulleted, 0, false)]
    [DataRow(NoteBlockKind.Numbered, 0, false)]
    [DataRow(NoteBlockKind.Checklist, 0, false)]
    [DataRow(NoteBlockKind.Bulleted, 5, false)]
    [DataRow(NoteBlockKind.Numbered, 5, false)]
    [DataRow(NoteBlockKind.Checklist, 5, false)]
    [DataRow(NoteBlockKind.Bulleted, 10, false)]
    [DataRow(NoteBlockKind.Numbered, 10, false)]
    [DataRow(NoteBlockKind.Checklist, 10, false)]
    [DataRow(NoteBlockKind.Bulleted, 5, true)]
    [DataRow(NoteBlockKind.Numbered, 5, true)]
    [DataRow(NoteBlockKind.Checklist, 5, true)]
    public void InlineListFragmentPreservesDestinationParagraphAndCheckedState(NoteBlockKind kind, int start, bool checkedTarget)
    {
        var document = NotesDocument.FromText("Task word tail").SetListKind(0, 14, kind)
            .SetStyle(0, 14, NoteTextStyle.Bold, true);
        var destination = NotesDocument.FromText("Hello target end");
        if (checkedTarget) destination = destination.SetListKind(0, 16, NoteBlockKind.Checklist).SetChecklistChecked(0, true);
        NotesDocument? saved = null;
        var source = CreateChecklistWindow(_ => { }, document: document);
        var target = CreateChecklistWindow(result => saved = result, document: destination);
        try
        {
            var editor = (RichTextBox)source.FindName("Editor");
            var run = ((Paragraph)((System.Windows.Documents.List)editor.Document.Blocks.FirstBlock)
                .ListItems.FirstListItem.Blocks.FirstBlock).Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart.GetPositionAtOffset(start)!, run.ContentStart.GetPositionAtOffset(start + 4)!);
            var data = CopySelection(editor);
            var targetEditor = (RichTextBox)target.FindName("Editor");
            var paragraph = checkedTarget
                ? (Paragraph)((System.Windows.Documents.List)targetEditor.Document.Blocks.FirstBlock).ListItems.FirstListItem.Blocks.FirstBlock
                : (Paragraph)targetEditor.Document.Blocks.FirstBlock;
            var targetRun = paragraph.Inlines.OfType<Run>().Single();
            targetEditor.Selection.Select(targetRun.ContentStart.GetPositionAtOffset(6)!, targetRun.ContentStart.GetPositionAtOffset(12)!);
            PasteSelection(targetEditor, data);
            Assert.IsNotNull(saved);
            var fragment = document.GetPlainText().Substring(start, 4);
            Assert.AreEqual($"Hello {fragment} end", saved.GetPlainText());
            Assert.AreEqual(destination.Paragraphs[0].Kind, saved.Paragraphs.Single().Kind);
            Assert.AreEqual(checkedTarget, saved.Paragraphs[0].IsChecked);
            Assert.AreEqual(NoteTextStyle.Bold, saved.Paragraphs[0].Runs.Single(item => item.Text == fragment).Style);
        }
        finally { source.CloseWithoutMembershipChange(); target.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("\r")]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void MultilineListCopyClipsRunsAfterPrecedingContent(string lineBreak)
    {
        var document = new NotesDocument
        {
            Paragraphs =
            [
                new() { Runs = [new NoteTextRun("Before")] },
                new() { Kind = NoteBlockKind.Bulleted, Runs = [new NoteTextRun("First" + lineBreak + "soft", NoteTextStyle.Bold)] },
                new() { Kind = NoteBlockKind.Numbered, Runs = [new NoteTextRun("Second", NoteTextStyle.Italic)] },
                new() { Runs = [new NoteTextRun("After")] }
            ]
        };
        var window = CreateChecklistWindow(_ => { }, document: document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var lists = editor.Document.Blocks.OfType<System.Windows.Documents.List>().ToArray();
            var first = ((Paragraph)lists[0].ListItems.FirstListItem.Blocks.FirstBlock).Inlines.OfType<Run>().First();
            var last = ((Paragraph)lists[1].ListItems.FirstListItem.Blocks.FirstBlock).Inlines.OfType<Run>().Single();
            editor.Selection.Select(first.ContentStart.GetPositionAtOffset(1)!, last.ContentStart.GetPositionAtOffset(4)!);
            var data = CopySelection(editor);
            var copied = System.Text.Json.JsonSerializer.Deserialize<NotesDocument>((string)data.GetData("Hood.Notes.List.v1")!)!;
            Assert.AreEqual("irst\r\nsoft\r\nSeco", copied.GetPlainText());
            CollectionAssert.AreEqual(new[] { NoteBlockKind.Bulleted, NoteBlockKind.Numbered }, copied.Paragraphs.Select(row => row.Kind).ToArray());
            // WPF represents a soft line break separately from the styled text runs.
            CollectionAssert.AreEqual(new[] { new NoteTextRun("irst", NoteTextStyle.Bold), new NoteTextRun("\r\n"),
                new NoteTextRun("soft", NoteTextStyle.Bold) }, copied.Paragraphs[0].Runs.ToArray());
            Assert.IsTrue(copied.Paragraphs[1].Runs.All(run => run.Style == NoteTextStyle.Italic));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static DataObject CopySelection(RichTextBox editor)
    {
        using var stream = new MemoryStream();
        editor.Selection.Save(stream, DataFormats.Xaml);
        var data = new DataObject(DataFormats.Xaml, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        editor.RaiseEvent(new DataObjectCopyingEventArgs(data, false));
        return data;
    }

    private static void PasteSelection(RichTextBox editor, DataObject data)
    {
        var paste = new DataObjectPastingEventArgs(data, false, DataFormats.Xaml);
        editor.RaiseEvent(paste);
        if (!paste.CommandCancelled)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes((string)data.GetData(DataFormats.Xaml)!));
            editor.Selection.Load(stream, DataFormats.Xaml);
        }
        editor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("oversized")]
    public void InvalidListPasteLeavesAcceptedContentUntouched(string scenario)
    {
        NotesDocument? saved = null;
        var window = CreateChecklistWindow(result => saved = result, document: NotesDocument.FromText("accepted"));
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            editor.CaretPosition = editor.Document.ContentEnd;
            var payload = scenario == "invalid" ? "{invalid" : System.Text.Json.JsonSerializer.Serialize(
                NotesDocument.FromText(new string('x', NotesSnapshotValidator.MaximumTextCharacters))
                    .SetListKind(0, 1, NoteBlockKind.Bulleted));
            var data = new DataObject("Hood.Notes.List.v1", payload);
            var paste = new DataObjectPastingEventArgs(data, false, "Hood.Notes.List.v1");
            editor.RaiseEvent(paste);
            Pump(window);
            Assert.IsTrue(paste.CommandCancelled);
            Assert.IsNull(saved);
            Assert.AreEqual("accepted\r\n", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            Assert.IsFalse(string.IsNullOrEmpty(((TextBlock)window.FindName("SaveStatus")).Text));
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
