using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DesktopPet.Core;
using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Windows;

public partial class NoteWindow
{
    private NoteInlineMathSourceSpan? _activeMathSource;
    private bool _inlineMathCommitPending;

    private void InitializeInlineMathEditing() =>
        Editor.SelectionChanged += OnInlineMathSelectionChanged;

    private System.Windows.Controls.MenuItem CreateDisplayMathMenuItem()
    {
        var item = new System.Windows.Controls.MenuItem { Header = "Create math block" };
        item.Click += (_, _) => CreateDisplayMathBlock();
        return item;
    }

    private void CreateDisplayMathBlock()
    {
        try
        {
            ApplyPendingMarkupFormatting();
            var document = ReadEditorDocument();
            var (start, length) = GetSelectionOffsets(document);
            NotesDocument updated;
            int blockIndex;
            if (length > 0)
            {
                if (!CanWrapInDisplayMath(document, start, length))
                {
                    SetSaveStatus("Select ordinary note text to create a display equation; the current content was left unchanged.");
                    return;
                }

                updated = NoteDisplayMathFormatting.ReplaceSelection(document, start, length, out blockIndex);
            }
            else
            {
                updated = NoteDisplayMathFormatting.InsertAtCaret(document, start, out blockIndex);
            }

            NotesSnapshotValidator.ValidateDocument(updated);
            var sourceStart = updated.GetParagraphRange(blockIndex).Start;
            SetEditorDocument(updated, sourceStart, 0);
            SetSaveStatus(_updateDocument(_noteId, updated));
            _lastAcceptedDocument = updated.Copy();
            UpdateToolbar();
            var paragraph = GetEditorParagraphs(Editor.Document).ElementAt(blockIndex);
            var button = Flatten(paragraph.Inlines).OfType<InlineUIContainer>()
                .Select(container => container.Child).OfType<NoteInlineMathButton>()
                .SingleOrDefault(candidate => candidate.IsDisplayMath);
            if (button is not null) BeginInlineMathSource(button);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
        }
    }

    private static bool CanWrapInDisplayMath(NotesDocument document, int start, int length)
    {
        var end = start + length;
        var (first, _) = document.GetParagraphPositionAt(start);
        var (last, _) = document.GetParagraphPositionAt(end);
        for (var index = first; index <= last; index++)
        {
            var row = document.Paragraphs[index];
            if (row.Cell is not null || row.Kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or
                NoteBlockKind.Checklist or NoteBlockKind.Code or NoteBlockKind.Divider or NoteBlockKind.DisplayMath)
                return false;
        }
        return true;
    }

    private InlineUIContainer CreateInlineMath(NoteTextRun noteRun)
    {
        var button = CreateMathButton(noteRun, isDisplayMath: false);
        return new InlineUIContainer(button) { BaselineAlignment = BaselineAlignment.Center };
    }

    private InlineUIContainer CreateDisplayMath(NoteTextRun noteRun)
    {
        var button = CreateMathButton(noteRun, isDisplayMath: true);
        return new InlineUIContainer(button) { BaselineAlignment = BaselineAlignment.Center };
    }

    private NoteInlineMathButton CreateMathButton(NoteTextRun noteRun, bool isDisplayMath)
    {
        var button = new NoteInlineMathButton(noteRun, Editor.FontSize, Editor.Foreground, isDisplayMath);
        button.SetResourceReference(System.Windows.Controls.Control.FocusVisualStyleProperty, "AppFocusAdorner");
        if (isDisplayMath)
            button.UpdateAppearance(Editor.FontSize, Editor.Foreground,
                GetDisplayMathMaxWidth(), GetDisplayMathMaxHeight());
        button.Click += (_, _) => BeginInlineMathSource(button);
        return button;
    }

    private void BeginInlineMathSource(NoteInlineMathButton button)
    {
        var container = GetEditorParagraphs(Editor.Document).SelectMany(paragraph => Flatten(paragraph.Inlines))
            .OfType<InlineUIContainer>().FirstOrDefault(inline => ReferenceEquals(inline.Child, button));
        if (container is null) return;

        var siblings = container.Parent switch
        {
            Paragraph paragraph => paragraph.Inlines,
            Span span => span.Inlines,
            _ => null
        };
        if (siblings is null) return;

        var source = new NoteInlineMathSourceSpan { IsDisplayMath = button.IsDisplayMath };
        if (button.NoteRun.Style.HasFlag(NoteTextStyle.Bold))
            source.FontWeight = FontWeights.Bold;
        if (button.NoteRun.Style.HasFlag(NoteTextStyle.Italic))
            source.FontStyle = FontStyles.Italic;
        source.TextDecorations = CreateDecorations(button.NoteRun.Style);
        var run = new Run(button.NoteRun.Text);
        source.Inlines.Add(run);
        siblings.InsertBefore(container, source);
        siblings.Remove(container);
        _activeMathSource = source;
        _inlineMathCommitPending = false;

        Editor.Focus();
        var caretOffset = button.IsDisplayMath
            ? GetDisplayMathSourceCaretOffset(button.NoteRun.Text)
            : button.NoteRun.Text.StartsWith('$') ? 1 : 0;
        var caret = run.ContentStart.GetPositionAtOffset(caretOffset, LogicalDirection.Forward) ?? run.ContentStart;
        Editor.CaretPosition = caret;
        source.BringIntoView();
    }

    private static int GetDisplayMathSourceCaretOffset(string source)
    {
        var opening = source.IndexOf("$$", StringComparison.Ordinal);
        if (opening < 0) return 0;
        var offset = opening + 2;
        if (offset < source.Length && source[offset] == '\r') offset++;
        if (offset < source.Length && source[offset] == '\n') offset++;
        return offset;
    }

    private bool InsertDisplayMathSourceLineBreak()
    {
        var source = _activeMathSource;
        if (source is not { IsDisplayMath: true } ||
            !IsInsideMathSource(Editor.Selection.Start, source) ||
            !IsInsideMathSource(Editor.Selection.End, source))
            return false;

        if (!Editor.Selection.IsEmpty) Editor.Selection.Text = string.Empty;
        Editor.CaretPosition = Editor.CaretPosition.InsertLineBreak();
        return true;
    }

    private bool InsertParagraphBesideDisplayMath()
    {
        var paragraph = Editor.CaretPosition.Paragraph;
        if (_activeMathSource is not null || !Editor.Selection.IsEmpty ||
            paragraph?.Tag is not NoteBlockKind.DisplayMath)
            return false;

        var document = ReadEditorDocument();
        var index = Array.IndexOf(GetEditorParagraphs(Editor.Document).ToArray(), paragraph);
        var range = document.GetParagraphRange(index);
        var before = Editor.CaretPosition.CompareTo(paragraph.Inlines.FirstInline.ContentStart) <= 0;
        document.Paragraphs.Insert(before ? index : index + 1, new NoteParagraph());
        var scrollPosition = CaptureEditorScrollPosition();
        SetEditorDocument(document, before ? range.Start : range.Start + range.Length + 2, 0);
        RestoreEditorScrollPosition(scrollPosition);
        SetSaveStatus(_updateDocument(_noteId, document));
        _lastAcceptedDocument = document.Copy();
        UpdateToolbar();
        return true;
    }

    private bool RemoveDisplayMathAtCaret(bool backwards)
    {
        if (_activeMathSource is not null) return false;
        var document = ReadEditorDocument();
        var (caret, selectionLength) = GetSelectionOffsets(document);
        var target = -1;
        if (selectionLength > 0)
        {
            var editorParagraphs = GetEditorParagraphs(Editor.Document).ToArray();
            for (var index = 0; index < editorParagraphs.Length; index++)
            {
                var paragraph = editorParagraphs[index];
                if (paragraph.Tag is not NoteBlockKind.DisplayMath) continue;
                var selectedContainer = Flatten(paragraph.Inlines).OfType<InlineUIContainer>()
                    .FirstOrDefault(container => container.Child is NoteInlineMathButton { IsDisplayMath: true } &&
                        Editor.Selection.Start.CompareTo(container.ContentStart) == 0 &&
                        Editor.Selection.End.CompareTo(container.ContentEnd) == 0);
                if (selectedContainer is not null)
                {
                    target = index;
                    break;
                }
            }

            var (first, _) = document.GetParagraphPositionAt(caret);
            var (last, _) = document.GetParagraphPositionAt(caret + selectionLength);
            if (target < 0 && first == last && document.Paragraphs[first].Kind == NoteBlockKind.DisplayMath)
            {
                var range = document.GetParagraphRange(first);
                if (caret <= range.Start && caret + selectionLength >= range.Start + range.Length)
                    target = first;
            }
        }
        else
        {
            var (index, offset) = document.GetParagraphPositionAt(caret);
            var paragraphLength = document.GetParagraphRange(index).Length;
            if (document.Paragraphs[index].Kind == NoteBlockKind.DisplayMath)
                target = index;
            else if (backwards && offset == 0 && index > 0 &&
                     document.Paragraphs[index - 1].Kind == NoteBlockKind.DisplayMath)
                target = index - 1;
            else if (!backwards && offset == paragraphLength && index + 1 < document.Paragraphs.Count &&
                     document.Paragraphs[index + 1].Kind == NoteBlockKind.DisplayMath)
                target = index + 1;
        }
        if (target < 0) return false;

        var caretOffset = document.GetParagraphRange(target).Start;
        var updated = document.Copy();
        updated.Paragraphs.RemoveAt(target);
        if (updated.Paragraphs.Count == 0) updated.Paragraphs.Add(new NoteParagraph());
        caretOffset = Math.Min(caretOffset, updated.GetPlainText().Length);
        SetEditorDocument(updated, caretOffset, 0);
        SetSaveStatus(_updateDocument(_noteId, updated));
        _lastAcceptedDocument = updated.Copy();
        UpdateToolbar();
        return true;
    }

    private void OnInlineMathSelectionChanged(object sender, RoutedEventArgs e)
    {
        var source = _activeMathSource;
        if (source is null || _updatingEditor ||
            IsInsideMathSource(Editor.Selection.Start, source) &&
            IsInsideMathSource(Editor.Selection.End, source))
            return;

        if (_inlineMathCommitPending) return;
        _inlineMathCommitPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
        {
            _inlineMathCommitPending = false;
            var currentSource = _activeMathSource;
            if (currentSource is null || _updatingEditor ||
                IsInsideMathSource(Editor.Selection.Start, currentSource) &&
                IsInsideMathSource(Editor.Selection.End, currentSource))
                return;

            try
            {
                var document = ReadEditorDocument();
                var (start, length) = GetSelectionOffsets(document);
                _activeMathSource = null;
                SetEditorDocument(document, start, length);
                UpdateToolbar();
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
            {
                RejectEditorChange(exception.Message);
            }
        }));
    }

    private static bool IsInsideMathSource(TextPointer position, NoteInlineMathSourceSpan source) =>
        position.CompareTo(source.ContentStart) >= 0 && position.CompareTo(source.ContentEnd) <= 0;

    private void RefreshInlineMathAppearance()
    {
        var maxWidth = GetDisplayMathMaxWidth();
        var maxHeight = GetDisplayMathMaxHeight();
        foreach (var math in GetEditorParagraphs(Editor.Document)
                     .SelectMany(paragraph => Flatten(paragraph.Inlines))
                     .OfType<InlineUIContainer>()
                     .Select(container => container.Child)
                     .OfType<NoteInlineMathButton>())
            math.UpdateAppearance(Editor.FontSize, Editor.Foreground, maxWidth, maxHeight);
    }

    private double GetDisplayMathMaxWidth()
    {
        var viewer = Editor.Template.FindName("PART_ContentHost", Editor) as ScrollViewer;
        var width = viewer?.ViewportWidth > 0 ? viewer.ViewportWidth :
            Editor.ActualWidth - Editor.Padding.Left - Editor.Padding.Right;
        return Math.Max(1, width);
    }

    private double GetDisplayMathMaxHeight()
    {
        var viewer = Editor.Template.FindName("PART_ContentHost", Editor) as ScrollViewer;
        var height = viewer?.ViewportHeight > 0 ? viewer.ViewportHeight : Editor.ActualHeight;
        return Math.Max(1, height);
    }
}
