using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DesktopPet.Core;
using DesktopPet.Windows;
using static DesktopPet.Windows.Tests.NoteClipboardTestInput;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteContentContrastTests
{
    [TestMethod]
    [DataRow("light", UiStyleCatalog.StickyNotes)]
    [DataRow("dark", UiStyleCatalog.StickyNotes)]
    [DataRow("light", UiStyleCatalog.Minimal)]
    [DataRow("dark", UiStyleCatalog.Minimal)]
    [DataRow("light", UiStyleCatalog.Soft)]
    [DataRow("dark", UiStyleCatalog.Soft)]
    [DataRow("light", UiStyleCatalog.Studio)]
    [DataRow("dark", UiStyleCatalog.Studio)]
    public void TablesAndCodeStayDistinctAcrossNoteColorsAndThemeChanges(string theme, string style)
    {
        var id = Guid.NewGuid();
        var document = new NotesDocument { Paragraphs = [
            new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("var answer = 42;")] },
            new NoteParagraph { Runs = [new NoteTextRun("inline", NoteTextStyle.Code)] },
            new NoteParagraph { Cell = new NoteTableCell(id, 0, 0), Runs = [new NoteTextRun("Header")] },
            new NoteParagraph { Cell = new NoteTableCell(id, 1, 0), Runs = [new NoteTextRun("Body")] }
        ] };
        var saves = 0;
        var window = new NoteWindow(new HoodNote { Document = document }, (_, _) => { saves++; return null; },
            (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration { Theme = theme }, style);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var code = (Paragraph)editor.Document.Blocks.FirstBlock;
            var inline = ((Paragraph)editor.Document.Blocks.ElementAt(1)).Inlines.OfType<Run>().Single();
            var table = editor.Document.Blocks.OfType<Table>().Single();
            var header = table.RowGroups[0].Rows[0].Cells[0];
            var body = table.RowGroups[0].Rows[1].Cells[0];
            var variants = new Dictionary<(string Theme, string Surface), HashSet<Color>>();
            var spacing = new Dictionary<string, (double Cell, double Code)>();
            // Reuse the same document to verify that existing content updates dynamically.
            foreach (var currentTheme in new[] { theme, theme == "light" ? "dark" : "light", theme })
            foreach (var currentStyle in UiStyleCatalog.Supported)
            foreach (var color in NoteColorPalette.Colors)
            {
                window.ApplyAppearance(new NotesConfiguration { Theme = currentTheme }, color, currentStyle);
                spacing[currentStyle] = (header.Padding.Left, code.Padding.Left);
                var noteFill = ((SolidColorBrush)window.Background).Color;
                var tableFill = Composite(table.Background, noteFill);
                var headerFill = Composite(header.Background, tableFill);
                var codeFill = Composite(code.Background, noteFill);
                var inlineFill = Composite(inline.Background, noteFill);
                foreach (var (name, fill) in new[] { ("code", codeFill), ("inline", inlineFill), ("table", tableFill),
                    ("header", headerFill), ("border", ((SolidColorBrush)table.BorderBrush).Color) })
                {
                    var key = (currentTheme, name);
                    if (!variants.TryGetValue(key, out var fills)) variants[key] = fills = [];
                    fills.Add(fill);
                }
                if (currentTheme == "light")
                    Assert.IsTrue(Contrast(inlineFill, noteFill) >= 1.5, $"Inline code blends into note: {currentStyle}/{color}.");
                var text = ((SolidColorBrush)editor.Foreground).Color;
                Assert.IsTrue(Contrast(headerFill, tableFill) >= 1.5, $"Header blends into rows: {currentTheme}/{currentStyle}/{color}.");
                Assert.IsTrue(Contrast(Composite(header.BorderBrush, headerFill), headerFill) >= 3,
                    $"Grid blends into header: {currentTheme}/{currentStyle}/{color}.");
                Assert.IsTrue(Contrast(Composite(body.BorderBrush, tableFill), tableFill) >= 3);
                Assert.IsTrue(header.BorderThickness.Bottom >= 1 && table.BorderThickness.Left >= 1);
                Assert.IsTrue(Contrast(Composite(table.BorderBrush, tableFill), tableFill) >= 3);
                Assert.IsTrue(code.BorderThickness.Left >= 1, "Code blocks need a visible outline.");
                Assert.IsTrue(Contrast(Composite(code.BorderBrush, codeFill), codeFill) >= 3);
                foreach (var fill in new[] { headerFill, tableFill, codeFill, inlineFill })
                    Assert.IsTrue(Contrast(text, fill) >= 4.5, $"Content text lost contrast: {currentTheme}/{currentStyle}/{color}.");
            }
            foreach (var variant in variants)
                Assert.AreEqual(NoteColorPalette.Colors.Count * UiStyleCatalog.Supported.Count, variant.Value.Count,
                    $"{variant.Key.Surface} must follow both the UI style and note color in {variant.Key.Theme} mode.");
            Assert.IsTrue(spacing[UiStyleCatalog.Minimal].Cell < spacing[UiStyleCatalog.StickyNotes].Cell);
            Assert.IsTrue(spacing[UiStyleCatalog.Soft].Cell > spacing[UiStyleCatalog.StickyNotes].Cell);
            Assert.IsTrue(spacing[UiStyleCatalog.Minimal].Code < spacing[UiStyleCatalog.StickyNotes].Code);
            Assert.IsTrue(spacing[UiStyleCatalog.Soft].Code > spacing[UiStyleCatalog.StickyNotes].Code);
            Assert.AreEqual(0, saves, "Appearance changes must not edit the note document.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void MixedContentKeepsActiveMathSelectionAcrossAppearancePreviews()
    {
        var tableId = Guid.NewGuid();
        var longUrl = "https://example.com/" + string.Join("/", Enumerable.Repeat("long-path-segment", 8)) + "?view=notes#details";
        var alignedMath = "$$" + string.Join("+", Enumerable.Repeat("x", 120)) + "$$";

        var document = new NotesDocument { Paragraphs = [
            new NoteParagraph { Kind = NoteBlockKind.Heading1, Runs = [new NoteTextRun("Heading 1")] },
            new NoteParagraph { Kind = NoteBlockKind.Heading2, Runs = [new NoteTextRun("Heading 2")] },
            new NoteParagraph { Kind = NoteBlockKind.Heading3, Runs = [new NoteTextRun("Heading 3")] },
            new NoteParagraph { Kind = NoteBlockKind.Heading4, Runs = [new NoteTextRun("Heading 4")] },
            new NoteParagraph { Kind = NoteBlockKind.Heading5, Runs = [new NoteTextRun("Heading 5")] },
            new NoteParagraph { Kind = NoteBlockKind.Heading6, Runs = [new NoteTextRun("Heading 6")] },
            new NoteParagraph { Runs = [
                new NoteTextRun("Intro "),
                new NoteTextRun("bold italic", NoteTextStyle.Bold | NoteTextStyle.Italic),
                new NoteTextRun(" equation "),
                new NoteTextRun("$x^2$", NoteTextStyle.None, NoteInlineKind.InlineMath),
                new NoteTextRun(" fraction "),
                new NoteTextRun("$\\frac{1}{2}$", NoteTextStyle.None, NoteInlineKind.InlineMath),
                new NoteTextRun(" error "),
                new NoteTextRun("$\\unknowncommand$", NoteTextStyle.None, NoteInlineKind.InlineMath),
                new NoteTextRun(" "),
                new NoteTextRun(longUrl, NoteTextStyle.None, NoteInlineKind.Link, longUrl)
            ] },
            new NoteParagraph { Kind = NoteBlockKind.Bulleted, Runs = [new NoteTextRun("Bullet")] },
            new NoteParagraph { Kind = NoteBlockKind.Numbered, Runs = [new NoteTextRun("Number")] },
            new NoteParagraph { Kind = NoteBlockKind.Checklist, IsChecked = true,
                Runs = [new NoteTextRun("Checked "), new NoteTextRun("item", NoteTextStyle.Bold)] },
            new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("literal **markers** $$math$$")] },
            new NoteParagraph { Kind = NoteBlockKind.Divider },
            new NoteParagraph { Kind = NoteBlockKind.DisplayMath, Runs = [new NoteTextRun(alignedMath)] },
            new NoteParagraph { Kind = NoteBlockKind.DisplayMath },
            new NoteParagraph { Kind = NoteBlockKind.DisplayMath, Runs = [new NoteTextRun("$$\\unknowncommand$$")] },
            new NoteParagraph { Cell = new NoteTableCell(tableId, 0, 0), Runs = [new NoteTextRun("Header", NoteTextStyle.Bold)] },
            new NoteParagraph { Cell = new NoteTableCell(tableId, 0, 1), Runs = [new NoteTextRun("Other")] },
            new NoteParagraph { Cell = new NoteTableCell(tableId, 1, 0), Runs = [
                new NoteTextRun("value "), new NoteTextRun("$\\alpha$", NoteTextStyle.None, NoteInlineKind.InlineMath)] },
            new NoteParagraph { Cell = new NoteTableCell(tableId, 1, 1), Runs = [
                new NoteTextRun("table link", NoteTextStyle.None, NoteInlineKind.Link, longUrl)] }
        ] };
        var saves = 0;
        var window = new NoteWindow(new HoodNote { Document = document }, (_, _) => { saves++; return null; },
            (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration { Theme = "light", TextSize = 16 }, UiStyleCatalog.StickyNotes);
        try
        {
            window.Width = 240;
            window.Height = 180;
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var flowDocument = editor.Document;
            var headings = RootParagraphs(editor).Where(paragraph => paragraph.Tag is NoteBlockKind kind &&
                kind >= NoteBlockKind.Heading1 && kind <= NoteBlockKind.Heading6).ToArray();
            var toolbar = (StackPanel)window.FindName("FormattingToolbar");
            var toolbarTop = toolbar.TranslatePoint(new Point(0, 0), window).Y;
            var toolbarHeight = toolbar.ActualHeight;
            var scroll = (ScrollViewer)editor.Template.FindName("PART_ContentHost", editor);
            var semanticBefore = ReadEditorDocument(window);
            AssertMixedDocumentEqual(document, semanticBefore);

            var inlineButtons = EquationButtons(editor, "Edit inline equation source").ToArray();
            var displayButtons = EquationButtons(editor, "Edit display equation source").ToArray();
            Assert.AreEqual(3, inlineButtons.Length);
            Assert.AreEqual(3, displayButtons.Length);
            var activeButton = inlineButtons[0];
            Assert.IsTrue(activeButton.IsTabStop, "Equation source editing must be reachable by keyboard.");
            Assert.IsNotNull(activeButton.FocusVisualStyle, "Equation buttons need a visible keyboard focus cue.");
            Assert.AreEqual("Edit inline equation source",
                System.Windows.Automation.AutomationProperties.GetName(activeButton));
            var longDisplay = displayButtons[0];
            var displayViewer = (ScrollViewer)longDisplay.Content;
            Assert.AreEqual(System.Windows.Controls.ScrollBarVisibility.Auto, displayViewer.HorizontalScrollBarVisibility);
            Assert.AreEqual(System.Windows.Controls.ScrollBarVisibility.Auto, displayViewer.VerticalScrollBarVisibility);
            var emptyDisplay = displayButtons[1];
            var displayError = displayButtons[2];
            Assert.IsTrue(FindFormulaControl(displayError)!.HasError);

            activeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Pump(window);
            var source = MathSourceSpans(editor).Single();
            var sourceRun = source.Inlines.OfType<Run>().Single();
            Assert.AreEqual("$x^2$", sourceRun.Text);
            Assert.IsTrue(editor.IsKeyboardFocused);
            var selectedStart = sourceRun.ContentStart.GetPositionAtOffset(1)!;
            var selectedEnd = sourceRun.ContentStart.GetPositionAtOffset(3)!;
            editor.Selection.Select(selectedStart, selectedEnd);
            var selectionStartOffset = sourceRun.ContentStart.GetOffsetToPosition(editor.Selection.Start);
            var selectionEndOffset = sourceRun.ContentStart.GetOffsetToPosition(editor.Selection.End);
            scroll.ScrollToVerticalOffset(40);
            Pump(window);
            var scrollOffset = scroll.VerticalOffset;
            var links = RootParagraphs(editor).SelectMany(paragraph => paragraph.Inlines.OfType<Hyperlink>()).ToArray();
            Assert.AreEqual(1, links.Length);
            var linkMenu = links[0].ContextMenu!;
            Assert.AreEqual(2, linkMenu.Items.Count);

            var savesBeforeAppearance = saves;
            var index = 0;
            foreach (var theme in new[] { "light", "dark", "system" })
            foreach (var style in UiStyleCatalog.Supported)
            foreach (var color in NoteColorPalette.Colors)
            {
                var textSize = index++ % 2 == 0 ? 12 : 28;
                window.ApplyAppearance(new NotesConfiguration { Theme = theme, TextSize = textSize }, color, style);
                Pump(window);

                Assert.AreSame(flowDocument, editor.Document);
                Assert.AreSame(source, MathSourceSpans(editor).Single());
                Assert.AreEqual("$x^2$", sourceRun.Text);
                Assert.AreEqual("x^", editor.Selection.Text);
                Assert.AreEqual(selectionStartOffset, sourceRun.ContentStart.GetOffsetToPosition(editor.Selection.Start));
                Assert.AreEqual(selectionEndOffset, sourceRun.ContentStart.GetOffsetToPosition(editor.Selection.End));
                Assert.IsTrue(editor.IsKeyboardFocused, $"The editor lost focus for {theme}/{style}/{color}.");
                Assert.AreEqual((double)textSize, editor.FontSize, .01);
                Assert.AreEqual(6, headings.Length);
                var headingMultipliers = new[] { 2.0, 1.7, 1.45, 1.25, 1.1, 1.0 };
                for (var heading = 0; heading < headings.Length; heading++)
                    Assert.AreEqual(textSize * headingMultipliers[heading], headings[heading].FontSize, .01);
                Assert.IsTrue(headings.Zip(headings.Skip(1), (first, next) => first.FontSize > next.FontSize).All(value => value));

                var fraction = FindFormulaControl(inlineButtons[1])!;
                Assert.IsFalse(fraction.HasError);
                Assert.AreEqual((double)textSize, fraction.Scale, .01);
                Assert.AreEqual(editor.Foreground, fraction.Foreground);
                Assert.IsTrue(FindFormulaControl(inlineButtons[2])!.HasError);
                Assert.IsTrue(((TextBlock)emptyDisplay.Content).Text.Contains("Click to enter equation", StringComparison.Ordinal));
                Assert.AreEqual(editor.Foreground, ((TextBlock)emptyDisplay.Content).Foreground);
                Assert.IsTrue(double.IsFinite(displayViewer.MaxWidth) && displayViewer.MaxWidth <= scroll.ViewportWidth + 1);
                Assert.IsTrue(double.IsFinite(displayViewer.MaxHeight) && displayViewer.MaxHeight > 1);
                Assert.IsTrue(displayViewer.MaxHeight <= scroll.ViewportHeight + 1);

                var focusBrush = ((SolidColorBrush)window.Resources["AppFocusBrush"]).Color;
                Assert.AreEqual(focusBrush, ((SolidColorBrush)linkMenu.Resources["AppFocusBrush"]).Color);
                var background = ((SolidColorBrush)window.Background).Color;
                var lightBackground = (Color)ColorConverter.ConvertFromString(NoteColorPalette.GetBackgroundHex(color, false, style));
                var darkBackground = (Color)ColorConverter.ConvertFromString(NoteColorPalette.GetBackgroundHex(color, true, style));
                Assert.IsTrue(background == lightBackground || background == darkBackground,
                    $"The effective appearance dropped the note color {color} for {theme}/{style}.");
                Assert.AreEqual(scrollOffset, scroll.VerticalOffset, 1.0);
                Assert.AreEqual(toolbarTop, toolbar.TranslatePoint(new Point(0, 0), window).Y, .1);
                Assert.AreEqual(toolbarHeight, toolbar.ActualHeight, .1);
                Assert.AreEqual(240, window.Width, .1);
                Assert.AreEqual(180, window.Height, .1);
                AssertMixedDocumentEqual(document, ReadEditorDocument(window));
                Assert.AreEqual(savesBeforeAppearance, saves, "Appearance previews must not save or enter note history.");
            }

            window.ApplyAppearance(new NotesConfiguration { Theme = "light", TextSize = 16 }, NoteColor.Yellow,
                UiStyleCatalog.StickyNotes);
            Pump(window);
            Assert.AreSame(source, MathSourceSpans(editor).Single());
            Assert.AreEqual("x^", editor.Selection.Text, "Restoring a canceled preview must retain the active selection.");

            links = RootParagraphs(editor).SelectMany(paragraph => paragraph.Inlines.OfType<Hyperlink>()).ToArray();
            Assert.IsTrue(links.Single().Focus());
            Pump(window);
            var focusedLink = RootParagraphs(editor).SelectMany(paragraph => paragraph.Inlines.OfType<Hyperlink>()).Single();
            Assert.IsTrue(focusedLink.IsKeyboardFocused);
            AssertMixedDocumentEqual(document, ReadEditorDocument(window));
            var savesBeforeFocusedAppearance = saves;
            window.ApplyAppearance(new NotesConfiguration { Theme = "dark", TextSize = 20 }, NoteColor.Pink,
                UiStyleCatalog.Studio);
            Pump(window);
            Assert.IsTrue(focusedLink.IsKeyboardFocused);
            Assert.AreEqual(((SolidColorBrush)window.Resources["AppFocusBrush"]).Color,
                ((SolidColorBrush)focusedLink.Foreground).Color);
            Assert.AreEqual(((SolidColorBrush)window.Resources["AppFocusBrush"]).Color,
                ((SolidColorBrush)focusedLink.ContextMenu!.Resources["AppFocusBrush"]).Color);
            Assert.AreEqual(savesBeforeFocusedAppearance, saves);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static void AssertMixedDocumentEqual(NotesDocument expected, NotesDocument actual)
    {
        Assert.AreEqual(expected.Paragraphs.Count, actual.Paragraphs.Count);
        for (var index = 0; index < expected.Paragraphs.Count; index++)
        {
            Assert.AreEqual(expected.Paragraphs[index].Cell, actual.Paragraphs[index].Cell, $"Cell {index} changed.");
            Assert.AreEqual(expected.Paragraphs[index].Kind, actual.Paragraphs[index].Kind, $"Block {index} changed.");
            Assert.AreEqual(expected.Paragraphs[index].IsChecked, actual.Paragraphs[index].IsChecked, $"Checklist {index} changed.");
            CollectionAssert.AreEqual(expected.Paragraphs[index].Runs.ToArray(), actual.Paragraphs[index].Runs.ToArray(),
                $"Inline semantics changed in block {index}.");
        }
    }

    private static IEnumerable<Paragraph> RootParagraphs(RichTextBox editor) =>
        editor.Document.Blocks.OfType<Paragraph>();

    private static IEnumerable<Button> EquationButtons(RichTextBox editor, string automationName) =>
        RootParagraphs(editor).SelectMany(paragraph => paragraph.Inlines.OfType<InlineUIContainer>())
            .Select(container => container.Child).OfType<Button>()
            .Where(button => System.Windows.Automation.AutomationProperties.GetName(button) == automationName);

    private static IEnumerable<Span> MathSourceSpans(RichTextBox editor) =>
        RootParagraphs(editor).SelectMany(paragraph => paragraph.Inlines.OfType<Span>())
            .Where(span => span.GetType().Name == "NoteInlineMathSourceSpan");

    private static WpfMath.Controls.FormulaControl? FindFormulaControl(Button button) => button.Content switch
    {
        WpfMath.Controls.FormulaControl formula => formula,
        ScrollViewer { Content: WpfMath.Controls.FormulaControl formula } => formula,
        Panel panel => panel.Children.OfType<WpfMath.Controls.FormulaControl>().SingleOrDefault(),
        _ => null
    };

    private static NotesDocument ReadEditorDocument(NoteWindow window) =>
        (NotesDocument)typeof(NoteWindow).GetMethod("ReadEditorDocument",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, null)!;

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
    [TestMethod]
    public void MixedMarkdownPasteKeepsFormattingOutsideAndInsideTables()
    {
        const string link = "https://example.com/guide";
        const string legacyLiteral = "**old bold** *old italic* __old underline__ ~~old strike~~ # old heading --- $old$ $$old$$ https://example.com/old";
        const string source = "\r\n# Integrated note\r\n## Level 2\r\n### Level 3\r\n#### Level 4\r\n##### Level 5\r\n###### Level 6\r\n" +
            "**bold** and *italic* and __underlined__ and ~~removed~~ with $x^2$ and [Guide](" + link + ").\r\n" +
            "Invalid math $\\unknowncommand$; unfinished $unfinished\r\n" +
            "Plain https://example.org/path.\r\n- bullet with $y$\r\n- [ ] checked [reference](https://example.org)\r\n---\r\n" +
            "$$\\frac{1}{2}$$\r\n$$\\unknowncommand$$\r\n" +
            "```cs\r\n**code** $c$ [Code](https://code.example)\r\n```\r\n" +
            "| Name | Formula | Link | Literal |\r\n| --- | --- | --- | --- |\r\n" +
            "| **cell bold** | $z_1$ | [cell link](https://example.net) | # cell heading and &#42;&#42;literal markers&#42;&#42; |\r\n\r\n" +
            "$$\r\nunfinished display source\r\n";
        NotesDocument? accepted = null;
        var window = new NoteWindow(new HoodNote { Document = NotesDocument.FromText(legacyLiteral) },
            (_, document) => { accepted = document.Copy(); return null; },
            (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
            () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
            new NotesConfiguration { Theme = "light" }, UiStyleCatalog.StickyNotes);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = editor.Document.ContentEnd;
            PastePlainText(editor, source);
            Pump(window);

            var document = accepted ?? ReadEditorDocument(window);

            Assert.AreEqual(6, document.Paragraphs.Count(row => row.Kind is >= NoteBlockKind.Heading1 and <= NoteBlockKind.Heading6));
            Assert.IsTrue(document.Paragraphs.Any(row => row.Kind == NoteBlockKind.Heading1));
            Assert.IsTrue(document.Paragraphs.Any(row => row.Kind == NoteBlockKind.Heading6));
            Assert.IsTrue(document.Paragraphs.Any(row => row.Kind == NoteBlockKind.Divider));
            Assert.AreEqual(2, document.Paragraphs.Count(row => row.Kind == NoteBlockKind.DisplayMath));
            Assert.IsTrue(document.Paragraphs.Any(row => row.Kind == NoteBlockKind.Code &&
                string.Concat(row.Runs.Select(run => run.Text)).Contains("**code** $c$", StringComparison.Ordinal)));
            Assert.IsTrue(document.Paragraphs.Any(row => row.Kind == NoteBlockKind.Bulleted));
            Assert.IsTrue(document.Paragraphs.Any(row => row.Kind == NoteBlockKind.Checklist && row.IsChecked == false));
            Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Text == "bold" && run.Style.HasFlag(NoteTextStyle.Bold)));
            Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.InlineMath && run.Text == "$x^2$"));
            Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.InlineMath && run.Text == "$\\unknowncommand$"));
            Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link && run.LinkDestination == link));
            Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link && run.LinkDestination == "https://example.org/path"));
            Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Text && run.Text.Contains("$unfinished", StringComparison.Ordinal)));
            Assert.IsTrue(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Text && run.Text.Contains("unfinished display source", StringComparison.Ordinal)));
            Assert.IsTrue(document.Paragraphs[0].Runs.All(run => run.Kind == NoteInlineKind.Text));
            Assert.IsTrue(document.Paragraphs[0].Runs.Any(run => run.Text.Contains(legacyLiteral, StringComparison.Ordinal)));
            Assert.IsFalse(document.Paragraphs.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link && run.LinkDestination == "https://example.com/old"));
            Assert.IsTrue(EquationButtons(editor, "Edit inline equation source")
                .Any(button => FindFormulaControl(button)?.HasError == true));

            var tableRows = document.Paragraphs.Where(row => row.Cell is not null).ToArray();
            Assert.AreEqual(8, tableRows.Length);
            Assert.IsTrue(tableRows.SelectMany(row => row.Runs)
                .Any(run => run.Text == "cell bold" && run.Style.HasFlag(NoteTextStyle.Bold)));
            Assert.IsTrue(tableRows.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.InlineMath && run.Text == "$z_1$"));
            Assert.IsTrue(tableRows.SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link && run.LinkDestination == "https://example.net"));
            Assert.IsTrue(tableRows.Where(row => row.Cell!.Column == 3).SelectMany(row => row.Runs)
                .Any(run => run.Text.Contains("**literal markers**", StringComparison.Ordinal)));
            Assert.IsTrue(tableRows.SelectMany(row => row.Runs)
                .Any(run => run.Text.Contains("# cell heading", StringComparison.Ordinal)));
            Assert.IsFalse(tableRows.Any(row => row.Kind is NoteBlockKind.Heading1 or NoteBlockKind.Divider or NoteBlockKind.DisplayMath));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }
    private static Color Composite(Brush? brush, Color background)
    {
        if (brush is not SolidColorBrush solid) return background;
        var alpha = solid.Color.A / 255.0 * solid.Opacity;
        return Color.FromRgb(Blend(solid.Color.R, background.R), Blend(solid.Color.G, background.G), Blend(solid.Color.B, background.B));
        byte Blend(byte foreground, byte behind) => (byte)Math.Round(foreground * alpha + behind * (1 - alpha));
    }

    private static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    private static double Linear(byte value) => value / 255.0 <= .04045
        ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + .055) / 1.055, 2.4);
}
