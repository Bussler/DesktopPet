using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteTableInteractionTests
{
    private const string Markdown = "| Name | Status | Action Points |\r\n| --- | :---: | ---: |\r\n| Alice | Open | • Call<br>• Email |";

    [TestMethod]
    [DataRow("caret")]
    [DataRow("cell")]
    [DataRow("table")]
    public void CtrlEnterAfterPastedTablePreservesSelectionAndCreatesEditableBodyParagraph(string selection)
    {
        NotesDocument? saved = null;
        var window = Create(NotesDocument.FromText(""), document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Paste(editor, Markdown);
            Pump(window);
            var before = saved!.GetPlainText();
            var table = editor.Document.Blocks.OfType<Table>().Single();
            var paragraph = (Paragraph)table.RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock;
            if (selection == "caret") editor.CaretPosition = paragraph.ContentStart;
            else editor.Selection.Select(selection == "table" ? table.ContentStart : paragraph.ContentStart,
                selection == "table" ? table.ContentEnd : paragraph.ContentEnd);
            Handle(window, Key.Enter, true, false);
            Assert.IsNull(CellAt(editor.CaretPosition));
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs.Last().Kind);
            Assert.AreEqual(before + "\r\n", saved.GetPlainText());
            Type(editor, "below");
            Pump(window);
            Assert.AreEqual(before + "\r\nbelow", saved.GetPlainText());
            Assert.IsNull(saved.Paragraphs.Last().Cell);
            Handle(window, Key.Enter, true, false);
            Type(editor, "second");
            Pump(window);
            Assert.AreEqual(before + "\r\nbelow\r\nsecond", saved.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("column")]
    [DataRow("row")]
    [DataRow("nested")]
    public void UnsupportedXamlTableImportRestoresAcceptedContentAfterTheEditTransaction(string shape)
    {
        NotesDocument? saved = null;
        var window = Create(NotesDocument.FromText("accepted"), document => saved = document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var cell = new TableCell(new Paragraph(new Run("unsupported")));
            if (shape == "column") cell.ColumnSpan = 2;
            if (shape == "row") cell.RowSpan = 2;
            var row = new TableRow();
            row.Cells.Add(cell);
            var group = new TableRowGroup();
            group.Rows.Add(row);
            var table = new Table();
            table.RowGroups.Add(group);
            if (shape == "nested") cell.Blocks.Add(new Table());
            var imported = new FlowDocument(table);
            using var xaml = new MemoryStream();
            new TextRange(imported.ContentStart, imported.ContentEnd).Save(xaml, DataFormats.Xaml);
            xaml.Position = 0;
            new TextRange(editor.Document.ContentEnd, editor.Document.ContentEnd).Load(xaml, DataFormats.Xaml);
            Pump(window);
            Assert.IsNull(saved, "Unsupported imported content must not be persisted.");
            Assert.AreEqual("accepted\r\n", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            Assert.IsFalse(string.IsNullOrEmpty(((TextBlock)window.FindName("SaveStatus")).Text));
            editor.CaretPosition = editor.Document.ContentEnd;
            editor.Selection.Text = " after";
            Pump(window);
            Assert.AreEqual("accepted after", saved!.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void OversizedNativeEditRestoresAcceptedContentAfterTheEditTransaction()
    {
        NotesDocument? saved = null;
        var window = Create(NotesDocument.FromText("accepted"), document => saved = document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            editor.CaretPosition = editor.Document.ContentEnd;
            editor.Selection.Text = new string('x', NotesSnapshotValidator.MaximumTextCharacters);
            Pump(window);
            Assert.IsNull(saved);
            Assert.AreEqual("accepted\r\n", new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text);
            Assert.IsFalse(string.IsNullOrEmpty(((TextBlock)window.FindName("SaveStatus")).Text));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("Insert row below")]
    [DataRow("Insert column right")]
    [DataRow("Tab")]
    [DataRow("Enter")]
    [DataRow("Ctrl+Enter")]
    public void TableEditsAtTheTextLimitAreRejectedWithoutEscapingOrChangingContent(string action)
    {
        var source = new NotesDocument { Paragraphs = [
            new NoteParagraph { Runs = [new NoteTextRun(new string('x', NotesSnapshotValidator.MaximumTextCharacters - 3))] },
            new NoteParagraph { Cell = new NoteTableCell(Guid.NewGuid(), 0, 0), Runs = [new NoteTextRun("a")] }
        ] };
        NotesSnapshotValidator.ValidateDocument(source);
        var saves = 0;
        var window = Create(source, _ => saves++);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var accepted = editor.Document;
            var cell = accepted.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0];
            editor.CaretPosition = ((Paragraph)cell.Blocks.FirstBlock).ContentEnd;
            if (action.StartsWith("Insert", StringComparison.Ordinal))
            {
                var item = cell.ContextMenu.Items.OfType<MenuItem>().Single(item => (string)item.Header == action);
                typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(item, null);
            }
            else Handle(window, action == "Tab" ? Key.Tab : Key.Enter, action == "Ctrl+Enter", false);
            Pump(window);
            Assert.AreEqual(0, saves);
            Assert.AreSame(accepted, editor.Document);
            Assert.AreEqual("a", new TextRange(cell.ContentStart, cell.ContentEnd).Text.TrimEnd('\r', '\n'));
            Assert.AreSame(cell, CellAt(editor.CaretPosition));
            Assert.IsFalse(string.IsNullOrEmpty(((TextBlock)window.FindName("SaveStatus")).Text));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void UndoLiteralBackticksSurviveTableClipboardAndLaterTyping()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, new NotesDocument { Paragraphs = [
            new NoteParagraph { Cell = new NoteTableCell(Guid.NewGuid(), 0, 0), Runs = [new NoteTextRun("literal ")] }
        ] });
        var window = new NoteWindow(app.GetNote(id),
            (_, document) => { app.UpdateDocument(id, document); return null; }, (_, _) => null, (_, _) => null,
            (_, undo) => { var moved = undo ? app.Undo(id) : app.Redo(id); return (moved ? app.GetNote(id).Document : null, null); },
            _ => (true, null), () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        string markdown;
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0].ContentEnd;
            Type(editor, "`value` marker");
            Pump(window);
            Assert.AreEqual("literal value marker", app.GetNote(id).Document.GetPlainText());
            typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [true]);
            var literal = app.GetNote(id).Document;
            Assert.AreEqual("literal `value` marker", literal.GetPlainText());
            Assert.IsTrue(literal.Paragraphs.SelectMany(row => row.Runs).All(run => run.Style == NoteTextStyle.None));
            markdown = NoteTableMarkdown.Write(literal, literal.Paragraphs[0].Cell!.TableId);
        }
        finally { window.CloseWithoutMembershipChange(); }
        NotesDocument? saved = null;
        window = Create(NotesDocument.FromText(""), document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            Paste(editor, markdown);
            Pump(window);
            Assert.AreEqual("literal `value` marker", saved!.GetPlainText());
            Assert.IsTrue(saved.Paragraphs.SelectMany(row => row.Runs).All(run => run.Style == NoteTextStyle.None));
            editor.CaretPosition = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0].ContentEnd;
            Type(editor, " after ");
            Pump(window);
            Assert.AreEqual("literal `value` marker after ", saved.GetPlainText());
            Type(editor, "`new` suffix");
            Pump(window);
            Assert.AreEqual("literal `value` marker after new suffix", saved.GetPlainText());
            CollectionAssert.AreEqual(new[] { "new" }, saved.Paragraphs.SelectMany(row => row.Runs)
                .Where(run => run.Style.HasFlag(NoteTextStyle.Code)).Select(run => run.Text).ToArray());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CreateTableIsInEditorRightClickMenuAlongsideStandardEditingCommandsAndAbsentFromHeader()
    {
        var window = Create(NotesDocument.FromText("text"), _ => { });
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var items = editor.ContextMenu.Items.OfType<MenuItem>().ToArray();
            CollectionAssert.AreEqual(new[] { "Cu_t", "_Copy", "_Paste", "Create _table", "Create math block" }, items.Select(item => (string)item.Header).ToArray());
            Assert.AreSame(ApplicationCommands.Cut, items[0].Command);
            Assert.AreSame(ApplicationCommands.Copy, items[1].Command);
            Assert.AreSame(ApplicationCommands.Paste, items[2].Command);
            Assert.IsTrue(items.Take(3).All(item => item.CommandTarget == editor));
            var header = ((System.Windows.Controls.Button)window.FindName("MenuButton")).ContextMenu;
            Assert.IsFalse(header.Items.OfType<MenuItem>().Any(item => (string)item.Header == "Create _table"));
            Assert.IsFalse(header.Items.OfType<MenuItem>().Any(item => (string)item.Header == "Create math block"));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("", 0)]
    [DataRow("before after", 7)]
    [DataRow("text", 0)]
    [DataRow("text", 4)]
    public void CreateTableMenuInsertsOneEditableCellAtCaretAndPreservesText(string text, int offset)
    {
        NotesDocument? saved = null;
        var source = NotesDocument.FromText(text).SetStyle(0, text.Length, NoteTextStyle.Bold, true);
        var window = Create(source, result => saved = result);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.CaretPosition = text.Length == 0 ? editor.Document.ContentEnd :
                ((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Single().ContentStart.GetPositionAtOffset(offset)!;
            ClickCreateTable(window);
            Assert.IsNotNull(saved);
            var table = editor.Document.Blocks.OfType<Table>().Single();
            Assert.AreEqual(1, table.Columns.Count);
            Assert.AreEqual(1, table.RowGroups[0].Rows.Count);
            Assert.AreEqual(1, table.RowGroups[0].Rows[0].Cells.Count);
            Assert.AreSame(table.RowGroups[0].Rows[0].Cells[0], CellAt(editor.CaretPosition));
            Assert.IsTrue(editor.IsKeyboardFocusWithin);
            Assert.AreEqual(text, string.Concat(saved.Paragraphs.Where(row => row.Cell is null).SelectMany(row => row.Runs).Select(run => run.Text)));
            Assert.IsTrue(saved.Paragraphs.Where(row => row.Cell is null).SelectMany(row => row.Runs).All(run => run.Style == NoteTextStyle.Bold));
            Type(editor, "Name");
            Pump(window);
            Assert.AreEqual("Name", saved.Paragraphs.Single(row => row.Cell is not null).Runs.Single().Text);
            Press(editor, Key.Tab);
            Assert.AreEqual(2, saved.Paragraphs.Where(row => row.Cell is not null).Select(row => row.Cell!.Row).Distinct().Count());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CreateTableInsideAnotherBlockInsertsAfterItWithoutNesting(bool insideTable)
    {
        NotesDocument? saved = null;
        var source = insideTable ? Parse() : new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("code")] }]
        };
        var window = Create(source, result => saved = result);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.CaretPosition = insideTable ? editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0].ContentStart :
                ((Paragraph)editor.Document.Blocks.FirstBlock).ContentStart;
            ClickCreateTable(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual(source.Paragraphs.Count + 1, saved.Paragraphs.Count);
            Assert.AreEqual(source.GetPlainText(), string.Join("\r\n", saved.Paragraphs.Take(source.Paragraphs.Count).Select(row => string.Concat(row.Runs.Select(run => run.Text)))));
            Assert.AreEqual(0, saved.Paragraphs.Last().Cell!.Row);
            Assert.AreEqual(0, saved.Paragraphs.Last().Cell!.Column);
            Assert.AreNotEqual(source.Paragraphs[0].Cell?.TableId, saved.Paragraphs.Last().Cell!.TableId);
            Assert.AreSame(editor.Document.Blocks.OfType<Table>().Last().RowGroups[0].Rows[0].Cells[0], CellAt(editor.CaretPosition));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void PasteRendersEditableCellsWithBulletListsAndContextActions()
    {
        NotesDocument? saved = null;
        var window = Create(NotesDocument.FromText(""), result => saved = result);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            Paste(editor, Markdown);
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.Paragraphs.All(row => row.Cell is not null));
            var table = editor.Document.Blocks.OfType<Table>().Single();
            Assert.AreEqual(2, table.RowGroups[0].Rows.Count);
            var cell = table.RowGroups[0].Rows[1].Cells[2];
            var list = (System.Windows.Documents.List)cell.Blocks.FirstBlock;
            Assert.AreEqual(TextMarkerStyle.Disc, list.MarkerStyle);
            Assert.AreEqual(2, list.ListItems.Count);
            Assert.AreEqual(TextAlignment.Right, cell.TextAlignment);
            CollectionAssert.AreEqual(new[] { "Insert row above", "Insert row below", "Insert column left", "Insert column right",
                "Delete row", "Delete column", "Copy table as Markdown", "Create _table", "Create math block" },
                cell.ContextMenu.Items.OfType<MenuItem>().Select(item => (string)item.Header).ToArray());
            Assert.IsNotNull(table.RowGroups[0].Rows[0].Cells[0].Background);
            Assert.IsNull(table.RowGroups[0].Rows[1].Cells[0].ReadLocalValue(TextElement.BackgroundProperty) as System.Windows.Media.Brush);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void TypedTableWaitsUntilTheTrailingBlankLine()
    {
        var window = Create(NotesDocument.FromText(""), _ => { });
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            editor.Selection.Text = Markdown.Replace("Alice", "`Alice`", StringComparison.Ordinal);
            Pump(window);
            Assert.AreEqual(0, editor.Document.Blocks.OfType<Table>().Count());
            editor.CaretPosition = editor.Document.ContentEnd;
            editor.Selection.Text = "\r\n";
            Pump(window);
            Assert.AreEqual(1, editor.Document.Blocks.OfType<Table>().Count());
            Assert.IsInstanceOfType<Paragraph>(editor.Document.Blocks.LastBlock);
            var cell = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[0];
            var code = ((Paragraph)cell.Blocks.FirstBlock).Inlines.OfType<Run>().Single();
            Assert.AreEqual("Alice", code.Text);
            Assert.AreEqual("Consolas", code.FontFamily.Source);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void KeyboardNavigationAddsRowsContinuesBulletsAndExitsTheTable()
    {
        NotesDocument? saved = null;
        var window = Create(Parse(), result => saved = result);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var table = editor.Document.Blocks.OfType<Table>().Single();
            editor.CaretPosition = table.RowGroups[0].Rows[0].Cells[0].ContentStart;
            Press(editor, Key.Tab);
            Assert.AreEqual(table.RowGroups[0].Rows[0].Cells[1], CellAt(editor.CaretPosition));
            Handle(window, Key.Tab, control: false, shift: true);
            Assert.AreEqual(table.RowGroups[0].Rows[0].Cells[0], CellAt(editor.CaretPosition));
            var actions = table.RowGroups[0].Rows[1].Cells[2];
            var last = (Paragraph)((System.Windows.Documents.List)actions.Blocks.FirstBlock).ListItems.LastListItem.Blocks.FirstBlock;
            editor.CaretPosition = last.ContentEnd;
            Press(editor, Key.Enter);
            Type(editor, "Review");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("Review", saved.Paragraphs.Last().Runs.Single().Text);
            Assert.AreEqual(NoteBlockKind.Bulleted, saved.Paragraphs.Last().Kind);
            Press(editor, Key.Tab);
            Assert.AreEqual(3, saved.Paragraphs.Select(row => row.Cell!.Row).Distinct().Count());
            Assert.AreEqual(2, saved.Paragraphs.Last().Cell!.Row);
            Type(editor, "Bob");
            Pump(window);
            Assert.AreEqual("Bob", saved.Paragraphs.First(row => row.Cell?.Row == 2).Runs.Single().Text);
            Handle(window, Key.Enter, control: true, shift: false);
            Type(editor, "after");
            Pump(window);
            Assert.IsNull(saved.Paragraphs.Last().Cell);
            Assert.AreEqual("after", saved.Paragraphs.Last().Runs.Single().Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ContextMenuEditsPreserveRectangularTableAndContent()
    {
        NotesDocument? saved = null;
        var window = Create(Parse(), result => saved = result);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            Click("Insert row below");
            Assert.IsNotNull(saved);
            Assert.AreEqual(3, saved.Paragraphs.Select(row => row.Cell!.Row).Distinct().Count());
            Click("Insert column right");
            Assert.AreEqual(4, saved.Paragraphs.Select(row => row.Cell!.Column).Distinct().Count());
            Click("Delete column");
            Click("Delete row");
            NotesSnapshotValidator.ValidateDocument(saved);
            Assert.IsTrue(saved.GetPlainText().Contains("Call\r\nEmail", StringComparison.Ordinal));

            void Click(string label)
            {
                var table = editor.Document.Blocks.OfType<Table>().Single();
                var menu = table.RowGroups[0].Rows[0].Cells[0].ContextMenu;
                var item = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == label);
                typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(item, null);
                Pump(window);
            }
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(240, "light")]
    [DataRow(600, "dark")]
    public void TablesFitTheEditorAndKeepStylesWhenResized(int width, string theme)
    {
        var window = Create(Parse(), _ => { }, theme);
        try
        {
            window.Width = width;
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var table = editor.Document.Blocks.OfType<Table>().Single();
            var available = editor.ActualWidth - editor.Padding.Left - editor.Padding.Right;
            Assert.IsTrue(table.Columns.Sum(column => column.Width.Value) <= available);
            Assert.AreEqual(table.Columns[0].Width, table.Columns[2].Width);
            var longCell = table.RowGroups[0].Rows[1].Cells[0];
            editor.CaretPosition = longCell.ContentStart;
            Type(editor, "A long name with many words that should wrap inside a narrow column ");
            Pump(window);
            var paragraph = (Paragraph)longCell.Blocks.FirstBlock;
            Assert.IsTrue(paragraph.ContentEnd.GetCharacterRect(LogicalDirection.Backward).Top >
                paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top);
            var header = table.RowGroups[0].Rows[0].Cells.Last();
            var end = ((Paragraph)header.Blocks.LastBlock).ContentEnd.GetCharacterRect(LogicalDirection.Backward);
            var content = (FrameworkElement)window.Content;
            var sample = editor.TranslatePoint(new Point(end.Right + 2, end.Top + end.Height / 2), content);
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var pixels = new byte[bitmap.PixelWidth * 4];
            bitmap.CopyPixels(new Int32Rect(0, (int)sample.Y, bitmap.PixelWidth, 1), pixels, pixels.Length, 0);
            var border = ((SolidColorBrush)table.BorderBrush).Color;
            Assert.IsTrue(Enumerable.Range((int)sample.X, bitmap.PixelWidth - (int)sample.X).Any(x =>
                pixels[x * 4] == border.B && pixels[x * 4 + 1] == border.G && pixels[x * 4 + 2] == border.R),
                "The table's right outline must render inside the editor instead of being clipped.");
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void InlineFormattingAndCodeWorkInsideCellsWithoutLosingTheirPositions()
    {
        NotesDocument? saved = null;
        var window = Create(Parse(), result => saved = result);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            var cell = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[0];
            var paragraph = (Paragraph)cell.Blocks.FirstBlock;
            editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);
            typeof(ToggleButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke((ToggleButton)window.FindName("BoldButton"), null);
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.Paragraphs.Single(row => row.Cell?.Row == 1 && row.Cell.Column == 0).Runs.All(run => run.Style.HasFlag(NoteTextStyle.Bold)));
            editor.CaretPosition = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[1].ContentEnd;
            editor.Selection.Text = " `code`";
            Pump(window);
            Assert.IsTrue(saved.Paragraphs.Single(row => row.Cell?.Row == 1 && row.Cell.Column == 1).Runs.Any(run => run.Style.HasFlag(NoteTextStyle.Code)));
            NotesSnapshotValidator.ValidateDocument(saved);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void WholeTableSelectionIsRecognizedWithoutDiscardingSurroundingText()
    {
        var source = Parse();
        var window = Create(source, _ => { });
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            editor.SelectAll();
            Assert.AreEqual(source.Paragraphs[0].Cell!.TableId, SelectedTable());
            var table = editor.Document.Blocks.OfType<Table>().Single();
            var preceding = new Paragraph(new Run("before"));
            editor.Document.Blocks.InsertBefore(table, preceding);
            editor.Selection.Select(((Run)preceding.Inlines.FirstInline).ContentStart.GetPositionAtOffset(2)!, table.ContentEnd);
            Assert.IsNull(SelectedTable());
            editor.Selection.Select(table.ContentStart, table.ContentEnd);
            Assert.AreEqual(source.Paragraphs[0].Cell!.TableId, SelectedTable());

            object? SelectedTable() => typeof(NoteWindow).GetMethod("GetSelectedTable", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void TypingEmphasisMathAndLinkTogetherKeepsThemInTheSameTableCell()
    {
        NotesDocument? saved = null;
        var tableId = Guid.NewGuid();
        var window = Create(new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Cell = new NoteTableCell(tableId, 0, 0) }]
        }, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var cell = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0];
            editor.CaretPosition = cell.ContentStart;
            editor.Selection.Text = "**bold** $x^2$ [label](https://example.com/a_(b)?q=x)";
            Pump(window);

            Assert.IsNotNull(saved);
            Assert.AreEqual("bold $x^2$ label", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold, saved.Paragraphs.Single().Runs[0].Style);
            Assert.AreEqual(NoteInlineKind.InlineMath,
                saved.Paragraphs.Single().Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Kind);
            Assert.AreEqual("$x^2$", saved.Paragraphs.Single().Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);
            var link = saved.Paragraphs.Single().Runs.Single(run => run.Kind == NoteInlineKind.Link);
            Assert.AreEqual("label", link.Text);
            Assert.AreEqual("https://example.com/a_(b)?q=x", link.LinkDestination);
            Assert.AreEqual(tableId, saved.Paragraphs.Single().Cell!.TableId);
            Assert.AreEqual(1, editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells.Count);
            NotesSnapshotValidator.ValidateDocument(saved);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void CopyingTableAsMarkdownAndPastingItKeepsMixedSemanticCellContent()
    {
        var tableDocument = MixedTable();
        var sourceTableId = tableDocument.Paragraphs[0].Cell!.TableId;
        var markdown = NoteTableMarkdown.Write(tableDocument, sourceTableId);
        IDataObject? copiedData = null;
        var copyWindow = Create(MixedEditorDocument(tableDocument), _ => { }, copyToClipboard: data => copiedData = data);
        NotesDocument? saved = null;
        var pasteWindow = Create(NotesDocument.FromText(""), document => saved = document);
        try
        {
            ShowHidden(copyWindow);
            var copyEditor = (RichTextBox)copyWindow.FindName("Editor");
            Assert.IsTrue(copyEditor.Focus());
            var sourceTable = copyEditor.Document.Blocks.OfType<Table>().Single();
            copyEditor.Selection.Select(sourceTable.ContentStart, sourceTable.ContentEnd);
            ApplicationCommands.Copy.Execute(null, copyEditor);
            Pump(copyWindow);
            Assert.IsNotNull(copiedData);
            Assert.AreEqual(markdown, copiedData.GetData(DataFormats.UnicodeText));
            Assert.AreEqual(markdown, copiedData.GetData(DataFormats.Text));

            ShowHidden(pasteWindow);
            var pasteEditor = (RichTextBox)pasteWindow.FindName("Editor");
            Assert.IsTrue(pasteEditor.Focus());
            NoteClipboardTestInput.Paste(pasteEditor, copiedData);
            Pump(pasteWindow);

            Assert.IsNotNull(saved);
            Assert.AreEqual(tableDocument.GetPlainText(), saved.GetPlainText());
            Assert.AreNotEqual(sourceTableId, saved.Paragraphs[0].Cell!.TableId);
            Assert.AreEqual(tableDocument.Paragraphs.Count, saved.Paragraphs.Count);
            for (var index = 0; index < tableDocument.Paragraphs.Count; index++)
            {
                var original = tableDocument.Paragraphs[index];
                var pasted = saved.Paragraphs[index];
                Assert.AreEqual(original.Kind, pasted.Kind);
                Assert.AreEqual(original.IsChecked, pasted.IsChecked);
                Assert.AreEqual(original.Cell!.Row, pasted.Cell!.Row);
                Assert.AreEqual(original.Cell.Column, pasted.Cell.Column);
                Assert.AreEqual(original.Cell.Alignment, pasted.Cell.Alignment);
                CollectionAssert.AreEqual(ExpandRuns(original), ExpandRuns(pasted));
            }
            NotesSnapshotValidator.ValidateDocument(saved);

            var checklist = ((System.Windows.Documents.List)
                editorTable().RowGroups[0].Rows[1].Cells[1].Blocks.FirstBlock);
            var checklistParagraph = (Paragraph)checklist.ListItems.FirstListItem.Blocks.FirstBlock;
            var checkBox = checklistParagraph.Inlines.OfType<InlineUIContainer>()
                .Select(container => container.Child).OfType<CheckBox>().Single();
            typeof(ToggleButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(checkBox, null);
            Pump(pasteWindow);
            var updatedItem = saved.Paragraphs.Single(row => row.Cell?.Row == 1 && row.Cell.Column == 1);
            Assert.IsFalse(updatedItem.IsChecked);
            CollectionAssert.AreEqual(tableDocument.Paragraphs.Single(row => row.Cell?.Row == 1 && row.Cell.Column == 1)
                .Runs.ToArray(), updatedItem.Runs.ToArray());

            Table editorTable() => pasteEditor.Document.Blocks.OfType<Table>().Single();
        }
        finally
        {
            copyWindow.CloseWithoutMembershipChange();
            pasteWindow.CloseWithoutMembershipChange();
        }
    }

    [TestMethod]
    public void PastingMarkdownCopyWithoutSemanticClipboardPreservesLiteralMarkers()
    {
        var source = MixedTable();
        var markdown = NoteTableMarkdown.Write(source, source.Paragraphs[0].Cell!.TableId);
        NotesDocument? saved = null;
        var window = Create(NotesDocument.FromText(""), document => saved = document);
        try
        {
            ShowHidden(window);
            Paste((RichTextBox)window.FindName("Editor"), markdown);
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual(source.GetPlainText(), saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Italic, saved.Paragraphs[2].Runs[0].Style);
            Assert.AreEqual(NoteInlineKind.InlineMath, saved.Paragraphs[2].Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Kind);
            Assert.AreEqual("$|x|$", saved.Paragraphs[2].Runs.Single(run => run.Kind == NoteInlineKind.InlineMath).Text);
            Assert.AreEqual("link", saved.Paragraphs[2].Runs.Single(run => run.Kind == NoteInlineKind.Link).Text);
            Assert.IsTrue(saved.Paragraphs[2].Runs.Any(run =>
                run.Text.Contains("**literal** $source$ [text](https://literal.example)", StringComparison.Ordinal)));
            Assert.IsTrue(saved.Paragraphs[3].IsChecked);
            Assert.AreEqual(NoteBlockKind.Checklist, saved.Paragraphs[3].Kind);
            Assert.IsFalse(saved.Paragraphs.Skip(2).SelectMany(row => row.Runs)
                .Any(run => run.Kind == NoteInlineKind.Link && run.LinkDestination == "https://literal.example"));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void UndoAfterAddingTheFinalRowKeepsTheCaretInsideTheRestoredDocument()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, Parse());
        var window = new NoteWindow(app.GetNote(id),
            (_, document) => { app.UpdateDocument(id, document); return null; }, (_, _) => null, (_, _) => null,
            (_, undo) => { var moved = undo ? app.Undo(id) : app.Redo(id); return (moved ? app.GetNote(id).Document : null, null); },
            _ => (true, null), () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var lastCell = editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[1].Cells[2];
            editor.CaretPosition = lastCell.ContentEnd;
            Press(editor, Key.Tab);
            Assert.AreEqual(3, editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows.Count);
            typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [true]);
            Assert.AreEqual(2, editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows.Count);
            Assert.IsTrue(editor.CaretPosition.CompareTo(editor.Document.ContentEnd) <= 0);
            typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [false]);
            Assert.AreEqual(3, editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows.Count);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private sealed class MemoryNotesStore : INotesStore
    {
        public NotesSnapshot? Load() => null;
        public void Save(NotesSnapshot snapshot) { }
    }

    [TestMethod]
    public void CreateTableMenuSupportsApplicationUndoAndRedo()
    {
        var app = NotesApplication.Start(new MemoryNotesStore());
        var id = app.CreateNote();
        app.UpdateDocument(id, NotesDocument.FromText("text"));
        var window = new NoteWindow(app.GetNote(id),
            (_, document) => { app.UpdateDocument(id, document); return null; }, (_, _) => null, (_, _) => null,
            (_, undo) => { var moved = undo ? app.Undo(id) : app.Redo(id); return (moved ? app.GetNote(id).Document : null, null); },
            _ => (true, null), () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration());
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            editor.CaretPosition = editor.Document.ContentEnd;
            ClickCreateTable(window);
            Assert.AreEqual(1, app.GetNote(id).Document.Paragraphs.Count(row => row.Cell is not null));
            typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [true]);
            Assert.AreEqual("text", app.GetNote(id).Document.GetPlainText());
            Assert.AreEqual(0, editor.Document.Blocks.OfType<Table>().Count());
            typeof(NoteWindow).GetMethod("RestoreHistory", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [false]);
            Assert.AreEqual(1, editor.Document.Blocks.OfType<Table>().Count());
            Assert.AreEqual(1, app.GetNote(id).Document.Paragraphs.Count(row => row.Cell is not null));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static NoteWindow Create(NotesDocument document, Action<NotesDocument> save, string theme = "dark",
        Action<IDataObject>? copyToClipboard = null) => new(
        new HoodNote { Document = document }, (_, updated) => { NotesSnapshotValidator.ValidateDocument(updated); save(updated.Copy()); return null; },
        (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
        () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null, new NotesConfiguration { Theme = theme },
        copyToClipboard: copyToClipboard);

    private static NotesDocument Parse()
    {
        var start = Markdown.Length;
        var length = 0;
        return NoteTableMarkdown.Apply(NotesDocument.FromText(Markdown), true, ref start, ref length);
    }
    private static NotesDocument MixedTable()
    {
        var tableId = Guid.NewGuid();
        var destination = "https://example.com/a_(b)?q=x&value=y#part";
        var document = new NotesDocument
        {
            Paragraphs = [
                new NoteParagraph { Cell = new(tableId, 0, 0), Runs = [new NoteTextRun("Tasks")] },
                new NoteParagraph { Cell = new(tableId, 0, 1), Runs = [new NoteTextRun("Notes")] },
                new NoteParagraph
                {
                    Cell = new(tableId, 1, 0), Kind = NoteBlockKind.Bulleted,
                    Runs = [
                        new NoteTextRun("styled", NoteTextStyle.Bold | NoteTextStyle.Italic),
                        new NoteTextRun(" "),
                        new NoteTextRun("$|x|$", NoteTextStyle.None, NoteInlineKind.InlineMath),
                        new NoteTextRun(" "),
                        new NoteTextRun("link", NoteTextStyle.Underline, NoteInlineKind.Link, destination),
                        new NoteTextRun("\ncontinued with **literal** $source$ [text](https://literal.example) \\| " + (char)92 + (char)92)
                    ]
                },
                new NoteParagraph
                {
                    Cell = new(tableId, 1, 1), Kind = NoteBlockKind.Checklist, IsChecked = true,
                    Runs = [
                        new NoteTextRun("checked ", NoteTextStyle.Strikethrough),
                        new NoteTextRun("$q$", NoteTextStyle.None, NoteInlineKind.InlineMath),
                        new NoteTextRun(" link", NoteTextStyle.Bold, NoteInlineKind.Link, destination),
                        new NoteTextRun("\nsecond line")
                    ]
                }
            ]
        };
        NotesSnapshotValidator.ValidateDocument(document);
        return document;
    }
    private static NotesDocument MixedEditorDocument(NotesDocument table)
    {
        var document = new NotesDocument
        {
            Paragraphs = [
                new NoteParagraph
                {
                    Kind = NoteBlockKind.Heading2,
                    Runs = [
                        new NoteTextRun("Editor heading ", NoteTextStyle.Bold),
                        new NoteTextRun("$h_1$", NoteTextStyle.None, NoteInlineKind.InlineMath),
                        new NoteTextRun(" "),
                        new NoteTextRun("source", NoteTextStyle.Italic, NoteInlineKind.Link, "https://example.com/heading")
                    ]
                },
                new NoteParagraph { Kind = NoteBlockKind.Bulleted, Runs = [new NoteTextRun("editor bullet")] },
                new NoteParagraph { Kind = NoteBlockKind.Numbered, Runs = [new NoteTextRun("editor number")] },
                new NoteParagraph { Kind = NoteBlockKind.Checklist, IsChecked = true, Runs = [new NoteTextRun("editor check")] },
                new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("**literal** $source$")] },
                new NoteParagraph { Kind = NoteBlockKind.Divider },
                new NoteParagraph { Kind = NoteBlockKind.DisplayMath, Runs = [new NoteTextRun("$$" + (char)92 + "sum_i i$$")] }
            ]
        };
        document.Paragraphs.AddRange(table.Paragraphs);
        NotesSnapshotValidator.ValidateDocument(document);
        return document;
    }
    private static void Paste(RichTextBox editor, string text)
    {
        editor.RaiseEvent(new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), false, DataFormats.UnicodeText));
        editor.Selection.Text = text;
    }
    private static TableCell? CellAt(TextPointer pointer)
    {
        for (var parent = pointer.Parent as FrameworkContentElement; parent is not null; parent = parent.Parent as FrameworkContentElement)
            if (parent is TableCell cell) return cell;
        return null;
    }
    private static (char Character, NoteTextStyle Style, NoteInlineKind Kind, string? Destination)[] ExpandRuns(NoteParagraph paragraph) =>
        paragraph.Runs.SelectMany(run => run.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Select(character => (character, run.Style, run.Kind, run.LinkDestination))).ToArray();
    private static void Press(RichTextBox editor, Key key) => editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
        PresentationSource.FromVisual(editor), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    private static void ClickCreateTable(NoteWindow window)
    {
        var editor = (RichTextBox)window.FindName("Editor");
        var menu = CellAt(editor.CaretPosition)?.ContextMenu ?? editor.ContextMenu;
        menu.PlacementTarget = editor;
        menu.Opacity = 0;
        menu.Placement = PlacementMode.Absolute;
        menu.HorizontalOffset = menu.VerticalOffset = -10000;
        menu.IsOpen = true;
        Pump(window);
        var item = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Create _table");
        typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(item, null);
        Pump(window);
        Assert.IsFalse(menu.IsOpen);
    }
    private static void Handle(NoteWindow window, Key key, bool control, bool shift) =>
        Assert.IsTrue((bool)typeof(NoteWindow).GetMethod("HandleEditorKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [key, control, shift])!);
    private static void Type(RichTextBox editor, string text) => TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, text));
    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.Left = window.Top = -10000;
        window.Show();
        window.Activate();
        Pump(window);
    }
    private static void Pump(Window window) => window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
}
