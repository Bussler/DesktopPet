using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Core;
using DesktopPet.Windows;

namespace DesktopPet.Windows.Tests;

[STATestClass]
[DoNotParallelize]
public sealed class NoteCodeInteractionTests
{
    [TestMethod]
    public void OpeningBackticksImmediatelyCreateFocusedEditableCode()
    {
        NotesDocument? saved = null;
        var window = CreateWindow(new NotesDocument(), document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            for (var index = 0; index < 3; index++)
            {
                Type(editor, "`");
                Pump(window);
                if (index < 2) Assert.AreEqual(NoteBlockKind.Paragraph, saved!.Paragraphs.Single().Kind);
            }
            Assert.AreEqual(NoteBlockKind.Code, saved!.Paragraphs.Single().Kind);
            Assert.AreEqual(string.Empty, saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, editor.CaretPosition.Paragraph!.Tag);
            Type(editor, "var x = 1;");
            Pump(window);
            editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Pump(window);
            Type(editor, "$$literal$$");
            Pump(window);
            Assert.AreEqual("var x = 1;\r\n$$literal$$", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs.Single().Kind);
            ExitCode(window);
            Type(editor, "after");
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs[1].Kind);
            Assert.AreEqual("after", string.Concat(saved.Paragraphs[1].Runs.Select(run => run.Text)));
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void EnterReplacesSelectedCodeWithALineBreakInTheSameBlock()
    {
        var source = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("first second third")] }]
        };
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock;
            var run = paragraph.Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart.GetPositionAtOffset(6)!, run.ContentStart.GetPositionAtOffset(12)!);
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            editor.RaiseEvent(key);
            Assert.IsTrue(key.Handled);
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("first \r\n third", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs.Single().Kind);
            Assert.AreSame(paragraph, editor.CaretPosition.Paragraph);
            Type(editor, "replacement");
            Pump(window);
            Assert.AreEqual("first \r\nreplacement third", saved.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(4)]
    public void EveryEnterInCodeImmediatelyMovesCaretToNextLine(int offset)
    {
        var source = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("code")] }]
        };
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var run = ((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Single();
            editor.CaretPosition = run.ContentStart.GetPositionAtOffset(offset)!;
            for (var press = 0; press < 2; press++)
            {
                var before = editor.CaretPosition.GetCharacterRect(editor.CaretPosition.LogicalDirection);
                editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Pump(window);
                var after = editor.CaretPosition.GetCharacterRect(editor.CaretPosition.LogicalDirection);
                Assert.IsTrue(after.Top > before.Top, $"Enter {press + 1} must move the caret down immediately: {before} -> {after}");
            }
            Type(editor, "next");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("code"[..offset] + "\r\n\r\nnext" + "code"[offset..], saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs.Single().Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeletingEntireInlineCodeReturnsFollowingTypingToOrdinaryText(bool backspace)
    {
        NotesDocument? saved = null;
        var source = NotesDocument.FromText("value").SetStyle(0, 5, NoteTextStyle.Code, true);
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var run = ((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Single();
            if (backspace)
            {
                editor.CaretPosition = run.ContentEnd;
                for (var i = 0; i < 5; i++) EditingCommands.Backspace.Execute(null, editor);
            }
            else
            {
                editor.Selection.Select(run.ContentStart, run.ContentEnd);
                EditingCommands.Delete.Execute(null, editor);
            }
            Pump(window);
            Type(editor, "ordinary");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("ordinary", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs.Single().Runs.Single().Style,
                "Deleting the last inline-code character must end code typing mode.");
            var ordinary = ((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Single();
            Assert.AreEqual(editor.FontFamily.Source, ordinary.FontFamily.Source);
            Assert.AreEqual((byte)0, ((SolidColorBrush)ordinary.Background).Color.A);
            EditingCommands.EnterParagraphBreak.Execute(null, editor);
            Type(editor, "line");
            Pump(window);
            Assert.AreEqual("ordinary\r\nline", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs.Last().Runs.Single().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeletingOneCodeSpanPreservesOtherSpansAndNonCodeFormatting(bool insideTable)
    {
        var source = new NotesDocument
        {
            Paragraphs = [new NoteParagraph
            {
                Cell = insideTable ? new NoteTableCell(Guid.NewGuid(), 0, 0) : null,
                Runs = [new NoteTextRun("keep", NoteTextStyle.Code | NoteTextStyle.Bold), new NoteTextRun(" ", NoteTextStyle.Bold),
                    new NoteTextRun("value", NoteTextStyle.Code | NoteTextStyle.Bold), new NoteTextRun(" tail", NoteTextStyle.Bold)]
            }]
        };
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var paragraph = insideTable ? (Paragraph)editor.Document.Blocks.OfType<Table>().Single().RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock :
                (Paragraph)editor.Document.Blocks.FirstBlock;
            var code = paragraph.Inlines.OfType<Run>().Single(run => run.Text == "value");
            editor.Selection.Select(code.ContentStart, code.ContentEnd);
            EditingCommands.Delete.Execute(null, editor);
            Pump(window);
            Type(editor, "ordinary");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("keep ordinary tail", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Code | NoteTextStyle.Bold, saved.Paragraphs.Single().Runs.Single(run => run.Text == "keep").Style);
            Assert.AreEqual(NoteTextStyle.Bold, saved.Paragraphs.Single().Runs.Single(run => run.Text.Contains("ordinary", StringComparison.Ordinal)).Style);
            Assert.AreEqual(source.Paragraphs[0].Cell, saved.Paragraphs[0].Cell);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void PartialInlineCodeDeletionKeepsCodeTypingAndOtherFormatting()
    {
        var source = NotesDocument.FromText("value").SetStyle(0, 5, NoteTextStyle.Code, true).SetStyle(0, 5, NoteTextStyle.Italic, true);
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var code = ((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Single();
            editor.CaretPosition = code.ContentStart.GetPositionAtOffset(3)!;
            EditingCommands.Backspace.Execute(null, editor);
            Type(editor, "x");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("vaxue", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Code | NoteTextStyle.Italic, saved.Paragraphs.Single().Runs.Single().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ClosingFenceLeavesCaretOutsideCodeForFollowingTyping()
    {
        NotesDocument? saved = null;
        var window = CreateWindow(NotesDocument.FromText("```\r\ncode\r\n``"), document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, "`");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs.Last().Kind,
                "Closing the fence must provide ordinary text after the code block.");
            Assert.AreSame(editor.Document.Blocks.LastBlock, editor.CaretPosition.Paragraph);
            Type(editor, "after");
            Pump(window);
            Assert.AreEqual("code\r\nafter", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs.Last().Runs.Single().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void DownArrowCommandAndEnterCanReachOrdinaryTextAfterClosedCode()
    {
        NotesDocument? saved = null;
        var window = CreateWindow(NotesDocument.FromText("```\r\ncode\r\n``"), document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, "`");
            Pump(window);
            editor.CaretPosition = ((Paragraph)editor.Document.Blocks.FirstBlock).ContentEnd;
            EditingCommands.MoveDownByLine.Execute(null, editor);
            Assert.AreSame(editor.Document.Blocks.LastBlock, editor.CaretPosition.Paragraph);
            EditingCommands.EnterParagraphBreak.Execute(null, editor);
            Type(editor, "after");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("code\r\n\r\nafter", saved.GetPlainText());
            Assert.IsTrue(saved.Paragraphs.Skip(1).All(row => row.Kind == NoteBlockKind.Paragraph));
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs.Last().Runs.Single().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    public void ClosingInlineBacktickFormatsCodeAndFollowingTypingUsesOrdinaryText(string theme)
    {
        NotesDocument? saved = null;
        var window = CreateWindow(NotesDocument.FromText("Use `value"), document => saved = document, theme);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            ShowHidden(window);
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = editor.Document.ContentEnd;
            Type(editor, "`");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("Use value", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Code, saved.Paragraphs[0].Runs.Last().Style);
            var code = ((Paragraph)editor.Document.Blocks.FirstBlock).Inlines.OfType<Run>().Single(run => run.Text == "value");
            Assert.AreEqual("Consolas", code.FontFamily.Source);
            Assert.IsInstanceOfType<SolidColorBrush>(code.Background);
            Assert.AreEqual((byte)255, ((SolidColorBrush)code.Background).Color.A);
            Assert.AreEqual(((SolidColorBrush)window.FindResource("NotesInlineCodeBrush")).Color,
                ((SolidColorBrush)code.Background).Color);

            Type(editor, " after");
            Pump(window);
            Assert.AreEqual("Use value after", saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[0].Runs.Last().Style);
            Assert.AreEqual(NoteTextStyle.Code, saved.Paragraphs[0].Runs.Single(run => run.Text == "value").Style);
            Assert.AreEqual(string.Empty, ((TextBlock)window.FindName("SaveStatus")).Text);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void PastedFencesCreateOneEditableBlockAndEnterKeepsNewlines()
    {
        NotesDocument? saved = null;
        var window = CreateWindow(NotesDocument.FromText(""), document => saved = document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            editor.Selection.Text = "```\r\n  first\r\n\tsecond `literal`\r\n```";
            editor.CaretPosition = editor.Document.ContentEnd;
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("  first\r\n\tsecond `literal`\r\n", saved.GetPlainText());
            Assert.AreEqual(2, saved.Paragraphs.Count);
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs[0].Kind);
            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock;
            Assert.AreEqual("Consolas", paragraph.FontFamily.Source);
            Assert.AreEqual(new Thickness(8), paragraph.Padding);
            Assert.IsInstanceOfType<SolidColorBrush>(paragraph.Background);

            ShowHidden(window);
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = paragraph.ContentEnd;
            editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor), 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            editor.Selection.Text = "  third";
            Assert.AreEqual("  first\r\n\tsecond `literal`\r\n  third\r\n", saved.GetPlainText());
            Assert.AreEqual(2, saved.Paragraphs.Count);
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs[0].Kind);

            ExitCode(window);
            Type(editor, "after");
            Pump(window);
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs[1].Kind);
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[1].Runs.Single().Style);
            Assert.AreEqual("  first\r\n\tsecond `literal`\r\n  third\r\nafter\r\n", saved.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void ReopenedCodeKeepsItsAppearanceAndSurvivesOtherFormattingAndThemeChanges()
    {
        var source = NotesDocument.FromText("Text `code`\r\n```\r\n  block\r\n```");
        var start = source.GetPlainText().Length;
        var length = 0;
        source = NoteCodeFormatting.Apply(source, ref start, ref length);
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            var editor = (RichTextBox)window.FindName("Editor");
            editor.SelectAll();
            var bold = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("BoldButton");
            typeof(System.Windows.Controls.Primitives.ToggleButton).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(bold, null);
            Assert.IsNotNull(saved);
            Assert.AreEqual(source.GetPlainText(), saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.Bold | NoteTextStyle.Code, saved.Paragraphs[0].Runs.Last().Style);
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs[1].Kind);
            window.ApplyAppearance(new NotesConfiguration { Theme = "light" }, NoteColor.Yellow);
            var block = editor.Document.Blocks.OfType<Paragraph>().Single(row => row.Tag is NoteBlockKind.Code);
            Assert.AreEqual("Consolas", block.FontFamily.Source);
            Assert.AreEqual((byte)255, ((SolidColorBrush)block.Background).Color.A);
            Assert.AreEqual(((SolidColorBrush)window.FindResource("NotesCodeBrush")).Color,
                ((SolidColorBrush)block.Background).Color);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExitReopenedCodeCreatesOrdinaryParagraphAndFollowingTypingIsPlain(bool existingParagraph)
    {
        var source = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("code")] }]
        };
        if (existingParagraph) source.Paragraphs.Add(new NoteParagraph());
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            editor.CaretPosition = ((Paragraph)editor.Document.Blocks.FirstBlock).ContentEnd;
            ExitCode(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual(existingParagraph ? 3 : 2, saved.Paragraphs.Count);
            Assert.AreSame(editor.Document.Blocks.ElementAt(1), editor.CaretPosition.Paragraph);
            Type(editor, "after");
            Pump(window);
            Assert.AreEqual("code\r\nafter" + (existingParagraph ? "\r\n" : ""), saved.GetPlainText());
            Assert.AreEqual(NoteTextStyle.None, saved.Paragraphs[1].Runs.Single().Style);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void MultilinePasteStaysInsideOneCodeBlockAndPreservesAllText(bool replaceSelection, bool minimized)
    {
        var text = minimized ? "a\r\n\r\nb" : "GeneratorDroneGymEnv.reset() reconfigures the child env with a freshly generated GymConfig on every reset (python/rads/gym/generator.py:71-80, 61-69). That fresh config has in_evaluation=False, eval_scenarios=None — so the eval env_config you pass (rllib_builders.py:169-183) is silently wiped. Consequences for generator runs (your experiment.yaml):\r\n\r\n  The configured eval scenarios (evaluation.scenarios: bns_iamd_scenario_distances.yaml) are never actually loaded — eval episodes run generated scenarios.\r\n\r\n  Reset info never contains scenario_path, so RAdsMetricsCallback._scenario_group falls back to eval_unknown_scenario for all eval metrics.";
        var source = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("prefix replace suffix")] },
                new NoteParagraph { Runs = [new NoteTextRun("neighbor")] }]
        };
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock;
            var run = paragraph.Inlines.OfType<Run>().Single();
            var start = run.ContentStart.GetPositionAtOffset(7)!;
            editor.Selection.Select(start, replaceSelection ? run.ContentStart.GetPositionAtOffset(14)! : start);
            NoteClipboardTestInput.PastePlainText(editor, text);
            Assert.IsNotNull(saved);
            Assert.AreEqual(2, saved.Paragraphs.Count, "Blank lines in pasted code must not create separate boxes.");
            var expected = "prefix " + text + (replaceSelection ? " suffix" : "replace suffix");
            Assert.AreEqual(expected + "\r\nneighbor", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs[0].Kind);
            Assert.AreEqual(NoteBlockKind.Paragraph, saved.Paragraphs[1].Kind);
            Type(editor, "typed");
            Pump(window);
            Assert.AreEqual("prefix " + text + "typed" + (replaceSelection ? " suffix" : "replace suffix") + "\r\nneighbor", saved.GetPlainText());
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow("\r\n", "UnicodeText")]
    [DataRow("\n", "Rich Text Format")]
    [DataRow("\r", "Hood.Notes.List.v1")]
    [DataRow("\r\n", "Hood.Notes.Semantic.v1")]
    public void PasteIntoEmptyCodeKeepsWhitespaceAndMarkupLiteral(string newline, string format)
    {
        const string normalized = " \r\n\r\n# heading\r\n- item\r\n`literal` $$x$$ https://example.com\r\n| a | b |\r\n|---|---|\r\n";
        var text = normalized.Replace("\r\n", newline, StringComparison.Ordinal);
        var source = new NotesDocument { Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code }] };
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var data = new DataObject(DataFormats.UnicodeText, text);
            data.SetData(DataFormats.Rtf, "{\\rtf1 different rich text}");
            var payload = System.Text.Json.JsonSerializer.Serialize(NotesDocument.FromText(text));
            data.SetData("Hood.Notes.List.v1", payload);
            data.SetData("Hood.Notes.Semantic.v1", payload);
            var paste = new DataObjectPastingEventArgs(data, false, format);
            editor.RaiseEvent(paste);
            Assert.IsTrue(paste.CommandCancelled, "Code paste must consume plain text before rich clipboard formats can split it.");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual(normalized, saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs.Single().Kind);
            Assert.IsTrue(saved.Paragraphs.Single().Runs.All(run => run.Kind == NoteInlineKind.Text && run.Style == NoteTextStyle.None));
            Assert.AreEqual(1, editor.Document.Blocks.Count);
            Assert.AreEqual(string.Empty, ((TextBlock)window.FindName("SaveStatus")).Text);
            // Reopening persisted content must retain one editable box, including the final blank line.
            typeof(NoteWindow).GetMethod("SetEditorDocument", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [saved, normalized.Length, 0]);
            Type(editor, "after");
            Pump(window);
            Assert.AreEqual(normalized + "after", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs.Single().Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    public void OversizedPasteIntoCodeLeavesContentAndSelectionUntouched()
    {
        var source = new NotesDocument
        {
            Paragraphs = [new NoteParagraph { Kind = NoteBlockKind.Code, Runs = [new NoteTextRun("accepted")] }]
        };
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock;
            var run = paragraph.Inlines.OfType<Run>().Single();
            editor.Selection.Select(run.ContentStart, run.ContentEnd);
            NoteClipboardTestInput.PastePlainText(editor, new string('x', NotesSnapshotValidator.MaximumTextCharacters + 1));
            Assert.IsNull(saved, "An oversized paste must not save a partial or invalid note.");
            Assert.AreSame(paragraph, editor.Document.Blocks.FirstBlock);
            Assert.AreEqual("accepted", run.Text);
            Assert.AreEqual("accepted", editor.Selection.Text);
            Assert.IsFalse(string.IsNullOrWhiteSpace(((TextBlock)window.FindName("SaveStatus")).Text));
            Type(editor, "valid");
            Pump(window);
            Assert.IsNotNull(saved);
            Assert.AreEqual("valid", saved.GetPlainText());
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs.Single().Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    [TestMethod]
    [DataRow(false, "X")]
    [DataRow(true, "X")]
    [DataRow(false, "X\r\nY")]
    public void PasteIntoVisibleCodePreservesScrolledViewport(bool replaceSelection, string text)
    {
        var source = new NotesDocument
        {
            Paragraphs = Enumerable.Range(0, 50).Select(index => new NoteParagraph
            {
                Kind = index == 25 ? NoteBlockKind.Code : NoteBlockKind.Paragraph,
                Runs = [new NoteTextRun(index == 25 ? "CODE" : $"line {index}")]
            }).ToList()
        };
        NotesDocument? saved = null;
        var window = CreateWindow(source, document => saved = document);
        try
        {
            ShowHidden(window);
            var editor = (RichTextBox)window.FindName("Editor");
            Assert.IsTrue(editor.Focus());
            var run = editor.Document.Blocks.OfType<Paragraph>().ElementAt(25).Inlines.OfType<Run>().Single();
            editor.Selection.Select(replaceSelection ? run.ContentStart.GetPositionAtOffset(1)! : run.ContentEnd,
                replaceSelection ? run.ContentStart.GetPositionAtOffset(3)! : run.ContentEnd);
            Pump(window);
            var viewer = (ScrollViewer)editor.Template.FindName("PART_ContentHost", editor);
            var caret = editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset + caret.Top - viewer.ViewportHeight / 2);
            Pump(window);
            var offset = viewer.VerticalOffset;
            caret = editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            Assert.IsTrue(offset > 0 && caret.Top >= 0 && caret.Bottom <= viewer.ViewportHeight,
                "The reproduction must paste at a visible caret in a scrolled note.");

            NoteClipboardTestInput.PastePlainText(editor, text);
            Pump(window);

            Assert.AreEqual(offset, viewer.VerticalOffset, 0.01, "Code paste must preserve the existing viewport.");
            var pastedCaret = editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            if (!text.Contains('\n')) Assert.AreEqual(caret.Top, pastedCaret.Top, 0.01);
            Assert.IsTrue(pastedCaret.Top >= 0 && pastedCaret.Bottom <= viewer.ViewportHeight);
            Assert.IsTrue(editor.Selection.IsEmpty);
            Assert.IsNotNull(saved);
            source.Paragraphs[25].Runs = [new NoteTextRun(replaceSelection ? "C" + text + "E" : "CODE" + text)];
            Assert.AreEqual(source.GetPlainText(), saved.GetPlainText());
            Assert.AreEqual(50, saved.Paragraphs.Count);
            Assert.AreEqual(NoteBlockKind.Code, saved.Paragraphs[25].Kind);
        }
        finally { window.CloseWithoutMembershipChange(); }
    }

    private static void ExitCode(NoteWindow window) => Assert.IsTrue((bool)typeof(NoteWindow)
        .GetMethod("HandleEditorKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [Key.Enter, true, false])!);

    private static NoteWindow CreateWindow(NotesDocument document, Action<NotesDocument> save, string theme = "dark") => new(
        new HoodNote { Document = document },
        (_, updated) => { NotesSnapshotValidator.ValidateDocument(updated); save(updated.Copy()); return null; },
        (_, _) => null, (_, _) => null, (_, _) => (null, null), _ => (true, null),
        () => { }, () => { }, () => { }, (_, _) => null, _ => null, () => null,
        new NotesConfiguration { Theme = theme });

    private static void ShowHidden(Window window)
    {
        window.Opacity = 0;
        window.Left = window.Top = -10000;
        window.Show();
        window.Activate();
        Pump(window);
    }

    private static void Pump(Window window) => window.Dispatcher.Invoke(() => { },
        System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private static void Type(RichTextBox editor, string text)
    {
        var composition = new TextComposition(InputManager.Current, editor, text);
        TextCompositionManager.StartComposition(composition);
    }
}
