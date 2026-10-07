using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using DesktopPet.Core;
using static DesktopPet.Core.NoteTextOffsets;

namespace DesktopPet.Windows;

public partial class NoteWindow : Window
{
    private static readonly System.Windows.Media.FontFamily CodeFont = new("Consolas");
    private readonly Guid _noteId;
    private readonly Func<Guid, NotesDocument, string?> _updateDocument;
    private readonly Func<Guid, string, string?> _updateName;
    private readonly Func<Guid, NoteBounds, string?> _updateBounds;
    private readonly Func<Guid, bool, (NotesDocument? Document, string? Error)> _editHistory;
    private readonly Func<Guid, (bool Allowed, string? Error)> _requestClose;
    private readonly Action _createNote;
    private readonly Action _showNotesList;
    private readonly Action _showNotesSettings;
    private readonly Func<Guid, NoteColor, string?> _updateNoteColor;
    private readonly Func<Guid, string?> _moveNoteToTrash;
    private readonly Func<string?> _retrySave;
    private readonly Action<string> _openLink;
    private readonly Action<System.Windows.IDataObject> _copyToClipboard;
    private NotesConfiguration _appearanceSettings;
    private string _uiStyle = UiStyleCatalog.StickyNotes;
    private NoteColor _noteColor;
    private HwndSource? _windowSource;
    private NoteBounds _restingBounds;
    private bool _appearanceInitialized;
    private NoteBounds? _moveResizeRestingBounds;
    private Action? _moveResizeTransitionCompletion;
    private Action? _transitionCompletion;
    private long _transitionRevision;
    private double _moveResizeStartLeft;
    private double _moveResizeStartTop;
    private bool _isTransitioning;
    private bool _applyingRecoveredBounds;
    private bool _isUserMoveResizeActive;
    private NotesDocument _lastAcceptedDocument;
    private bool _initializing = true;
    private bool _updatingEditor;
    private bool _markupFormattingPending;
    private string? _markupOriginalText;
    private bool _editorRecoveryPending;
    private bool _closingForApplicationExit;
    private bool _lookingUpLinkRange;

    public NoteWindow(
        HoodNote note,
        Func<Guid, NotesDocument, string?> updateDocument,
        Func<Guid, string, string?> updateName,
        Func<Guid, NoteBounds, string?> updateBounds,
        Func<Guid, bool, (NotesDocument? Document, string? Error)> editHistory,
        Func<Guid, (bool Allowed, string? Error)> requestClose,
        Action createNote,
        Action showNotesList,
        Action showNotesSettings,
        Func<Guid, NoteColor, string?> updateNoteColor,
        Func<Guid, string?> moveNoteToTrash,
        Func<string?> retrySave,
        NotesConfiguration appearanceSettings,
        string uiStyle = UiStyleCatalog.StickyNotes,
        Action<string>? openLink = null,
        Action<System.Windows.IDataObject>? copyToClipboard = null)
    {
        _noteId = note.Id;
        _updateDocument = updateDocument;
        _updateName = updateName;
        _updateBounds = updateBounds;
        _editHistory = editHistory;
        _requestClose = requestClose;
        _createNote = createNote;
        _showNotesList = showNotesList;
        _showNotesSettings = showNotesSettings;
        _updateNoteColor = updateNoteColor;
        _moveNoteToTrash = moveNoteToTrash;
        _retrySave = retrySave;
        _openLink = openLink ?? OpenLinkInDefaultBrowser;
        _copyToClipboard = copyToClipboard ?? (data => System.Windows.Clipboard.SetDataObject(data, true));
        _appearanceSettings = appearanceSettings;
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        _noteColor = note.Color;
        _restingBounds = note.Bounds;
        _lastAcceptedDocument = note.Document.Copy();
        InitializeComponent();
        InitializeInlineMathEditing();
        InitializeLinkEditing();
        NoteTitle.Text = note.DisplayName;
        Title = note.DisplayName;
        Left = note.Bounds.Left;
        Top = note.Bounds.Top;
        Width = note.Bounds.Width;
        Height = note.Bounds.Height;
        ApplyAppearance(appearanceSettings, note.Color, _uiStyle);
        SetEditorDocument(note.Document, null, null);
        MenuButton.ContextMenu = CreateHeaderMenu();
        InitializeTableEditing();
        InitializeListClipboard();
        _initializing = false;
    }

    public void CloseWithoutMembershipChange()
    {
        _closingForApplicationExit = true;
        Close();
    }

    public void SetSaveStatus(string? error)
    {
        SaveStatus.Text = error is null ? string.Empty : $"Not saved. {error}";
        SaveStatus.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        RetrySaveButton.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ApplyAppearance(NotesConfiguration settings, NoteColor color, string? uiStyle = null)
    {
        var updatingEditor = _updatingEditor;
        _updatingEditor = true;
        try { UpdateAppearance(settings, color, uiStyle); }
        finally { _updatingEditor = updatingEditor; }
    }

    private void UpdateAppearance(NotesConfiguration settings, NoteColor color, string? uiStyle)
    {
        var previousSettings = _appearanceSettings;
        var scrollPosition = CaptureEditorScrollPosition();
        _appearanceSettings = settings;
        _noteColor = color;
        if (uiStyle is not null)
        {
            _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        }

        var darkTheme = NotesAppearance.Apply(this, settings.Theme, _uiStyle);
        if (MenuButton.ContextMenu is { } menu)
        {
            NotesAppearance.Apply(menu, settings.Theme, _uiStyle);
        }
        RefreshTableMenus();
        RefreshLinkMenus();
        Background = CreateBrush(NoteColorPalette.GetBackgroundHex(color, darkTheme, _uiStyle));
        NotesAppearance.ApplyContent(this, darkTheme, color, _uiStyle);
        var foreground = CreateBrush(NoteColorPalette.GetForegroundHex(darkTheme));
        Foreground = foreground;
        NoteTitle.Foreground = foreground;
        NoteTitle.CaretBrush = foreground;
        Editor.Foreground = foreground;
        Editor.CaretBrush = foreground;
        Editor.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty,
            "AppSelectedBrush");
        Editor.SelectionOpacity = .45;
        if (!_appearanceInitialized || previousSettings.TextSize != settings.TextSize)
        {
            Editor.FontSize = settings.TextSize;
            Editor.Document.FontSize = settings.TextSize;
            foreach (var paragraph in GetEditorParagraphs(Editor.Document))
                if (paragraph.Tag is NoteBlockKind kind && GetHeadingLevel(kind) > 0)
                    paragraph.FontSize = GetHeadingFontSize(kind);
            foreach (var list in GetEditorBlocks(Editor.Document.Blocks).OfType<System.Windows.Documents.List>())
            {
                list.Padding = new Thickness(GetListMarkerWidth(), 0, 0, 0);
                foreach (var paragraph in list.ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>()))
                    ApplyInlineMarkerSpacing(paragraph);
            }
        }
        if (!_appearanceInitialized || previousSettings.TextSize != settings.TextSize || previousSettings.LineSpacing != settings.LineSpacing)
        {
            Editor.Document.LineHeight = GetEditorLineHeight();
            Editor.Document.LineStackingStrategy = GetEditorLineStackingStrategy();
            foreach (var paragraph in GetEditorParagraphs(Editor.Document))
                if (paragraph.Tag is NoteBlockKind kind && GetHeadingLevel(kind) > 0)
                    paragraph.LineHeight = GetEditorLineHeight(paragraph.FontSize);
        }
        if (!_appearanceInitialized || previousSettings.LineSpacing != settings.LineSpacing)
        {
            foreach (var paragraph in GetEditorBlocks(Editor.Document.Blocks).OfType<Paragraph>())
                paragraph.Margin = new Thickness(paragraph.Margin.Left, paragraph.Margin.Top, paragraph.Margin.Right, 4 * settings.LineSpacing);
        }
        SaveStatus.Foreground = CreateBrush(darkTheme ? "#FFFFB4AB" : "#FF8B1F1F");
        if (!_appearanceInitialized || previousSettings.AlwaysOnTop != settings.AlwaysOnTop)
        {
            Topmost = settings.AlwaysOnTop;
        }
        _appearanceInitialized = true;
        Editor.UpdateLayout();
        UpdateTableWidths();
        RefreshInlineMathAppearance();
        RestoreEditorScrollPosition(scrollPosition);
    }

    private (double Horizontal, double Vertical)? CaptureEditorScrollPosition() =>
        Editor.Template.FindName("PART_ContentHost", Editor) is System.Windows.Controls.ScrollViewer scrollViewer
            ? (scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset)
            : null;

    private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0) UpdateTableWidths();
        if (e.VerticalChange != 0 && sender is ScrollViewer viewer &&
            viewer.Template.FindName("PART_VerticalScrollBar", viewer) is System.Windows.Controls.Primitives.ScrollBar scrollbar)
        {
            scrollbar.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 1, TimeSpan.FromSeconds(1))
            {
                FillBehavior = FillBehavior.Stop
            });
        }
    }

    private void RestoreEditorScrollPosition((double Horizontal, double Vertical)? position)
    {
        if (position is null)
        {
            return;
        }

        Editor.ApplyTemplate();
        Editor.UpdateLayout();
        if (Editor.Template.FindName("PART_ContentHost", Editor) is System.Windows.Controls.ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToHorizontalOffset(position.Value.Horizontal);
            scrollViewer.ScrollToVerticalOffset(position.Value.Vertical);
            Editor.UpdateLayout();
            // WPF can bring the recreated selection into view after the current layout pass.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                scrollViewer.ScrollToHorizontalOffset(position.Value.Horizontal);
                scrollViewer.ScrollToVerticalOffset(position.Value.Vertical);
            }));
        }
    }

    public void SetGroupVisibilityImmediately(bool visible)
    {
        CancelPositionTransition(restoreRestingPosition: true);
        if (visible)
        {
            if (!IsVisible)
            {
                Show();
            }
        }
        else if (IsVisible)
        {
            Hide();
        }
    }

    public void ApplyRecoveredBounds(NoteBounds bounds)
    {
        NotesSnapshotValidator.ValidateBounds(bounds);
        _applyingRecoveredBounds = true;
        try
        {
            _restingBounds = bounds;
            CancelPositionTransition(restoreRestingPosition: true);
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }
        finally
        {
            _applyingRecoveredBounds = false;
        }
    }

    public void BeginGroupVisibilityTransition(bool visible, System.Windows.Point hoodCenter, TimeSpan duration, Action completed)
    {
        var currentLeft = Left;
        var currentTop = Top;
        CancelPositionTransition(restoreRestingPosition: true);
        if (!visible && !IsVisible)
        {
            completed();
            return;
        }

        var fromLeft = visible && !IsVisible ? hoodCenter.X - _restingBounds.Width / 2 : currentLeft;
        var fromTop = visible && !IsVisible ? hoodCenter.Y - _restingBounds.Height / 2 : currentTop;
        var toLeft = visible ? _restingBounds.Left : hoodCenter.X - _restingBounds.Width / 2;
        var toTop = visible ? _restingBounds.Top : hoodCenter.Y - _restingBounds.Height / 2;
        var revision = ++_transitionRevision;
        _isTransitioning = true;
        _transitionCompletion = completed;

        var xAnimation = CreatePositionAnimation(fromLeft, toLeft, duration, visible);
        var yAnimation = CreatePositionAnimation(fromTop, toTop, duration, visible);
        yAnimation.Completed += (_, _) =>
        {
            if (revision != _transitionRevision || !_isTransitioning)
            {
                return;
            }

            var finish = _transitionCompletion;
            CancelPositionTransition(restoreRestingPosition: true);
            finish?.Invoke();
        };

        BeginAnimation(LeftProperty, xAnimation, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(TopProperty, yAnimation, HandoffBehavior.SnapshotAndReplace);
        if (visible && !IsVisible)
        {
            Show();
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _updatingEditor || _editorRecoveryPending)
        {
            return;
        }

        try
        {
            var document = ReadEditorDocument();
            if (document.GetPlainText().Length > NotesSnapshotValidator.MaximumTextCharacters)
            {
                RejectEditorChange($"A note is limited to {NotesSnapshotValidator.MaximumTextCharacters:N0} characters; the extra text was not accepted.");
                return;
            }

            if (e.Changes.Any(change => change.RemovedLength > 0))
            {
                ResetDeletedInlineCodeTyping(document);
                ResetDeletedHeadingTyping(document);
            }
            var originalText = _lastAcceptedDocument.GetPlainText();
            var plainText = document.GetPlainText();
            TrackPlainUrlInput(document, GetInsertedRange(originalText, plainText));
            ClearLinkFeedback();
            SetSaveStatus(_updateDocument(_noteId, document));
            _lastAcceptedDocument = document.Copy();
            _updatingEditor = true;
            try { RefreshNumberedMarkers(); }
            finally { _updatingEditor = false; }
            if (!_markupFormattingPending && (_tablePasteRequested || _blockSyntaxPasteRequested ||
                plainText.IndexOfAny(['`', '|', '-', '[', ')', '*', '_', '~', '$', '#']) >= 0 ||
                plainText.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                plainText.Contains("https://", StringComparison.OrdinalIgnoreCase)))
            {
                _markupFormattingPending = true;
                _markupOriginalText = originalText;
                // TextChanged still runs inside WPF's edit transaction; replace the document after it ends.
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(ApplyPendingMarkupFormatting));
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            RejectEditorChange(exception.Message);
        }
    }

    private void RejectEditorChange(string error)
    {
        SetSaveStatus(error);
        if (_editorRecoveryPending) return;
        _editorRecoveryPending = true;
        _markupFormattingPending = false;
        _markupOriginalText = null;
        _tablePasteRequested = false;
        _blockSyntaxPasteRequested = false;
        // A document replacement inside TextChanged causes WPF to terminate the process.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
        {
            if (_editorRecoveryPending) SetEditorDocument(_lastAcceptedDocument, null, null);
        }));
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_editorRecoveryPending) return;
        try { UpdateToolbar(); }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            RejectEditorChange(exception.Message);
        }
        if (!_lookingUpLinkRange) CompletePlainUrlWhenCaretLeaves();
    }

    private void OnBoldClick(object sender, RoutedEventArgs e) => ApplyStyle(NoteTextStyle.Bold, BoldButton.IsChecked == true);
    private void OnItalicClick(object sender, RoutedEventArgs e) => ApplyStyle(NoteTextStyle.Italic, ItalicButton.IsChecked == true);
    private void OnUnderlineClick(object sender, RoutedEventArgs e) => ApplyStyle(NoteTextStyle.Underline, UnderlineButton.IsChecked == true);
    private void OnStrikethroughClick(object sender, RoutedEventArgs e) => ApplyStyle(NoteTextStyle.Strikethrough, StrikethroughButton.IsChecked == true);
    private void OnBulletsClick(object sender, RoutedEventArgs e) => ApplyListKind(NoteBlockKind.Bulleted);
    private void OnNumberedClick(object sender, RoutedEventArgs e) => ApplyListKind(NoteBlockKind.Numbered);
    private void OnChecklistClick(object sender, RoutedEventArgs e) => ApplyListKind(NoteBlockKind.Checklist);

    private void OnColorClick(NoteColor color)
    {
        var error = _updateNoteColor(_noteId, color);
        SetSaveStatus(error);
        ApplyAppearance(_appearanceSettings, color);
        MenuButton.ContextMenu = CreateHeaderMenu();
    }

    private void OnRetrySaveClick(object sender, RoutedEventArgs e) => SetSaveStatus(_retrySave());

    private void OnDeleteNoteClick()
    {
        var error = _moveNoteToTrash(_noteId);
        if (error is not null)
        {
            SetSaveStatus(error);
        }
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        SetSaveStatus(_updateName(_noteId, NoteTitle.Text));
        Title = string.IsNullOrWhiteSpace(NoteTitle.Text) ? "Note" : NoteTitle.Text;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (NoteTitle.IsKeyboardFocusWithin && e.Key == Key.Enter)
        {
            Editor.Focus();
            e.Handled = true;
            return;
        }

        if (!Editor.IsKeyboardFocusWithin)
        {
            return;
        }

        if (_editorRecoveryPending)
        {
            e.Handled = true;
            return;
        }

        e.Handled = HandleEditorKey(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

    private bool HandleEditorKey(Key key, bool control, bool shift)
    {
        if (control && !shift && key == Key.Z)
        {
            RestoreHistory(undo: true);
            return true;
        }
        else if (control && !shift && key == Key.Y)
        {
            RestoreHistory(undo: false);
            return true;
        }
        else if (control && shift && key == Key.X)
        {
            ApplyStyle(NoteTextStyle.Strikethrough, StrikethroughButton.IsChecked != true);
            return true;
        }
        else if (control && !shift && key == Key.B)
        {
            ApplyStyle(NoteTextStyle.Bold, BoldButton.IsChecked != true);
            return true;
        }
        else if (control && !shift && key == Key.I)
        {
            ApplyStyle(NoteTextStyle.Italic, ItalicButton.IsChecked != true);
            return true;
        }
        else if (control && !shift && key == Key.U)
        {
            ApplyStyle(NoteTextStyle.Underline, UnderlineButton.IsChecked != true);
            return true;
        }
        else if (control && !shift && key == Key.Enter)
        {
            return InsertParagraphAfterCurrentElement();
        }
        else if (HandleTableKey(key, control, shift))
        {
            return true;
        }
        else if (!control && !shift && key == Key.Enter && ContinueCodeAtCaret())
        {
            return true;
        }
        else if (!control && !shift && key == Key.Enter && ContinueHeadingAtCaret())
        {
            return true;
        }
        else if (!control && !shift && key == Key.Enter &&
                 (InsertDisplayMathSourceLineBreak() || InsertParagraphBesideDisplayMath()))
        {
            return true;
        }
        else if (!control && !shift && key == Key.Enter && Editor.Selection.IsEmpty && ContinueListAtCaret())
        {
            return true;
        }
        else if (!control && !shift && key == Key.Back &&
                 (RemoveChecklistAtCaret() || RemoveDividerAtCaret(backwards: true) || RemoveDisplayMathAtCaret(backwards: true)))
        {
            return true;
        }
        else if (!control && !shift && key == Key.Delete &&
                 (RemoveDividerAtCaret(backwards: false) || RemoveDisplayMathAtCaret(backwards: false)))
        {
            return true;
        }
        return false;
    }

    private void ApplyPendingMarkupFormatting()
    {
        if (!_markupFormattingPending) return;
        _markupFormattingPending = false;
        try
        {
            var document = ReadEditorDocument();
            var (start, length) = GetSelectionOffsets(document);
            var text = document.GetPlainText();
            var original = _markupOriginalText ?? text;
            var typingStyle = GetTypingStyle(document, start);
            _markupOriginalText = null;
            var opened = _blockSyntaxPasteRequested ? document :
                NoteBlockMarkdownFormatting.OpenTypedBlock(document, GetInsertedRange(original, text), ref start, length);
            var openedMath = !ReferenceEquals(opened, document) &&
                opened.Paragraphs[opened.GetParagraphIndexAt(start)].Kind == NoteBlockKind.DisplayMath;
            var formatted = NoteListFormatting.Apply(opened, GetInsertedRange(original, opened.GetPlainText()), ref start, ref length);
            formatted = NoteCodeFormatting.Apply(formatted, ref start, ref length, GetInsertedRange(original, formatted.GetPlainText()));
            var beforeTable = formatted;
            formatted = NoteTableMarkdown.Apply(formatted, _tablePasteRequested, ref start, ref length);
            var tableWasConverted = !ReferenceEquals(beforeTable, formatted);
            var temporarilyMaskedTableRuns = new HashSet<NoteTextRun>(ReferenceEqualityComparer.Instance);
            if (tableWasConverted)
            {
                foreach (var cellRow in formatted.Paragraphs.Where(row => row.Cell is not null))
                {
                    cellRow.Runs = cellRow.Runs.Select(run =>
                    {
                        if (run.Kind != NoteInlineKind.Text || run.Style.HasFlag(NoteTextStyle.Code)) return run;
                        var masked = run with { Style = run.Style | NoteTextStyle.Code };
                        temporarilyMaskedTableRuns.Add(masked);
                        return masked;
                    }).ToList();
                }
            }
            var emphasisConverted = false;
            // Table-cell parsing interprets inline syntax before decoding escaped literal markers.
            var emphasisInput = GetInsertedRange(original, formatted.GetPlainText());
            var beforeEmphasis = formatted;
            formatted = NoteEmphasisFormatting.Apply(formatted, emphasisInput, ref start, ref length);
            emphasisConverted = !ReferenceEquals(beforeEmphasis, formatted);
            var blockInput = GetInsertedRange(original, formatted.GetPlainText());
            formatted = NoteBlockMarkdownFormatting.Apply(formatted, blockInput, ref start, ref length,
                pasted: _blockSyntaxPasteRequested);
            var displayMathInput = GetInsertedRange(original, formatted.GetPlainText());
            formatted = NoteDisplayMathFormatting.Apply(formatted, displayMathInput, ref start, ref length);
            var linkInput = GetInsertedRange(original, formatted.GetPlainText());
            var completedPlainUrl = tableWasConverted ? null : _completedPlainUrl;
            _completedPlainUrl = null;
            formatted = NoteLinkFormatting.Apply(formatted, linkInput, ref start, ref length,
                pasted: _blockSyntaxPasteRequested, forcePlainUrl: completedPlainUrl);
            var mathInput = GetInsertedRange(original, formatted.GetPlainText());
            formatted = NoteInlineMathFormatting.Apply(formatted, mathInput, ref start, ref length);
            foreach (var cellRow in formatted.Paragraphs.Where(row => row.Cell is not null))
            {
                cellRow.Runs = cellRow.Runs.Select(run => temporarilyMaskedTableRuns.Contains(run)
                    ? run with { Style = run.Style & ~NoteTextStyle.Code }
                    : run).ToList();
            }
            _tablePasteRequested = false;
            _blockSyntaxPasteRequested = false;
            if (ReferenceEquals(formatted, document)) return;
            var scrollPosition = CaptureEditorScrollPosition();
            SetEditorDocument(formatted, start, length);
            RestoreEditorScrollPosition(scrollPosition);
            var (paragraphIndex, localOffset) = formatted.GetParagraphPositionAt(start);
            var row = formatted.Paragraphs[paragraphIndex];
            var runEnd = 0;
            var endsInlineCode = row.Runs.Any(run =>
            {
                runEnd += NormalizeLineBreaks(run.Text).Length;
                return runEnd == localOffset && run.Style.HasFlag(NoteTextStyle.Code);
            });
            if (length == 0 && row.Kind != NoteBlockKind.Code)
            {
                if (emphasisConverted) ApplyTypingEmphasis(typingStyle);
                if (endsInlineCode) ResetCodeTypingFormat();
            }
            SetSaveStatus(_updateDocument(_noteId, formatted));
            _lastAcceptedDocument = formatted.Copy();
            UpdateToolbar();
            if (openedMath)
            {
                var paragraph = GetEditorParagraphs(Editor.Document).ElementAt(paragraphIndex);
                var button = Flatten(paragraph.Inlines).OfType<InlineUIContainer>()
                    .Select(container => container.Child).OfType<NoteInlineMathButton>()
                    .Single(candidate => candidate.IsDisplayMath);
                BeginInlineMathSource(button);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
        }
    }

    private static NoteTextStyle GetTypingStyle(NotesDocument document, int offset) =>
        (offset > 0 ? GetStyleAtOffset(document, offset - 1) : null) ??
        GetStyleAtOffset(document, offset) ?? NoteTextStyle.None;

    private static NoteTextStyle? GetStyleAtOffset(NotesDocument document, int offset)
    {
        var paragraphStart = 0;
        for (var paragraphIndex = 0; paragraphIndex < document.Paragraphs.Count; paragraphIndex++)
        {
            var paragraph = document.Paragraphs[paragraphIndex];
            foreach (var run in paragraph.Runs)
            {
                var runLength = NormalizeLineBreaks(run.Text).Length;
                if (offset >= paragraphStart && offset < paragraphStart + runLength) return run.Style;
                paragraphStart += runLength;
            }
            if (paragraphIndex < document.Paragraphs.Count - 1) paragraphStart += 2;
        }
        return null;
    }

    private void ApplyTypingEmphasis(NoteTextStyle style)
    {
        _updatingEditor = true;
        try
        {
            Editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,
                style.HasFlag(NoteTextStyle.Bold) ? FontWeights.Bold : FontWeights.Normal);
            Editor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty,
                style.HasFlag(NoteTextStyle.Italic) ? FontStyles.Italic : FontStyles.Normal);
            Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, CreateDecorations(style));
        }
        finally { _updatingEditor = false; }
    }

    private static (int Start, int Length) GetInsertedRange(string original, string text)
    {
        var prefix = 0;
        while (prefix < Math.Min(original.Length, text.Length) && original[prefix] == text[prefix]) prefix++;
        var oldEnd = original.Length;
        var newEnd = text.Length;
        while (oldEnd > prefix && newEnd > prefix && original[oldEnd - 1] == text[newEnd - 1]) { oldEnd--; newEnd--; }
        return (prefix, newEnd - prefix);
    }

    private void ResetDeletedInlineCodeTyping(NotesDocument document)
    {
        var (start, length) = GetSelectionOffsets(document);
        var (index, offset) = document.GetParagraphPositionAt(start);
        var row = document.Paragraphs[index];
        if (length != 0 || row.Kind == NoteBlockKind.Code) return;
        var runEnd = 0;
        var touchesCode = row.Runs.Any(run =>
        {
            var runStart = runEnd;
            runEnd += NormalizeLineBreaks(run.Text).Length;
            return run.Style.HasFlag(NoteTextStyle.Code) && runStart <= offset && offset <= runEnd;
        });
        if (!touchesCode) ResetCodeTypingFormat();
    }

    private void ResetDeletedHeadingTyping(NotesDocument document)
    {
        if (!Editor.Selection.IsEmpty) return;
        var paragraphs = GetEditorParagraphs(Editor.Document).ToArray();
        var index = Array.IndexOf(paragraphs, Editor.CaretPosition.Paragraph);
        if (index < 0) return;
        var row = document.Paragraphs[index];
        if (GetHeadingLevel(row.Kind) == 0 || row.Runs.Any(run => run.Text.Length > 0)) return;
        row.Kind = NoteBlockKind.Paragraph;
        _updatingEditor = true;
        try
        {
            paragraphs[index].Tag = null;
            paragraphs[index].ClearValue(TextElement.FontSizeProperty);
            Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, Editor.FontSize);
            if (HasProperty(Editor.Selection, TextElement.FontWeightProperty, FontWeights.SemiBold) == true)
                Editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
        }
        finally { _updatingEditor = false; }
    }

    private void ResetCodeTypingFormat()
    {
        _updatingEditor = true;
        try
        {
            Editor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, Editor.FontFamily);
            Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        }
        finally { _updatingEditor = false; }
    }

    private bool ContinueHeadingAtCaret()
    {
        var document = ReadEditorDocument();
        var (start, length) = GetSelectionOffsets(document);
        var (paragraphIndex, localOffset) = document.GetParagraphPositionAt(start);
        if (length != 0 || GetHeadingLevel(document.Paragraphs[paragraphIndex].Kind) == 0) return false;

        var updated = document.SplitParagraph(paragraphIndex, localOffset);
        updated.Paragraphs[paragraphIndex + 1].Kind = NoteBlockKind.Paragraph;
        var scrollPosition = CaptureEditorScrollPosition();
        SetEditorDocument(updated, start + 2, 0);
        RestoreEditorScrollPosition(scrollPosition);
        SetSaveStatus(_updateDocument(_noteId, updated));
        _lastAcceptedDocument = updated.Copy();
        UpdateToolbar();
        return true;
    }
    private bool ContinueCodeAtCaret()
    {
        var document = ReadEditorDocument();
        var (start, length) = GetSelectionOffsets(document);
        var index = document.GetParagraphIndexAt(start);
        if (document.Paragraphs[index].Kind != NoteBlockKind.Code) return false;
        var (paragraphStart, paragraphLength) = document.GetParagraphRange(index);
        if (start + length > paragraphStart + paragraphLength) return false;
        EditingCommands.EnterLineBreak.Execute(null, Editor);
        return true;
    }

    private bool InsertParagraphAfterCurrentElement()
    {
        try
        {
            ApplyPendingMarkupFormatting();
            var document = ReadEditorDocument();
            var paragraphs = GetEditorParagraphs(Editor.Document).ToArray();
            var paragraph = paragraphs.FirstOrDefault(row => Flatten(row.Inlines).OfType<InlineUIContainer>()
                .Any(inline => inline.Child?.IsKeyboardFocusWithin == true)) ?? Editor.CaretPosition.Paragraph;
            var index = Array.IndexOf(paragraphs, paragraph);
            if (index < 0) index = document.GetParagraphIndexAt(GetSelectionOffsets(document).Start);
            var source = document.Paragraphs[index];
            if (source.Cell is { } cell)
                index = document.Paragraphs.FindLastIndex(row => row.Cell?.TableId == cell.TableId);
            else if (source.Kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist)
                while (index + 1 < document.Paragraphs.Count && document.Paragraphs[index + 1].Cell is null &&
                       document.Paragraphs[index + 1].Kind == source.Kind) index++;
            document.Paragraphs.Insert(index + 1, new NoteParagraph());
            CommitEditorDocument(document, document.GetParagraphRange(index + 1).Start);
            UpdateToolbar();
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
        }
        return true;
    }

    private void ApplyStyle(NoteTextStyle style, bool enabled)
    {
        if (Editor.Selection.IsEmpty)
        {
            _updatingEditor = true;
            try
            {
                if (style == NoteTextStyle.Bold)
                    Editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, enabled ? FontWeights.Bold : FontWeights.Normal);
                else if (style == NoteTextStyle.Italic)
                    Editor.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, enabled ? FontStyles.Italic : FontStyles.Normal);
                else
                {
                    var decorations = (Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection)?.Clone()
                        ?? new TextDecorationCollection();
                    var location = style == NoteTextStyle.Underline ? TextDecorationLocation.Underline : TextDecorationLocation.Strikethrough;
                    for (var index = decorations.Count - 1; index >= 0; index--)
                        if (decorations[index].Location == location) decorations.RemoveAt(index);
                    if (enabled) decorations.Add(style == NoteTextStyle.Underline ? TextDecorations.Underline[0] : TextDecorations.Strikethrough[0]);
                    Editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, decorations);
                }
            }
            finally { _updatingEditor = false; }
            UpdateToolbar();
            return;
        }

        var document = ReadEditorDocument();
        var (start, length) = GetSelectionOffsets(document);
        start = Math.Clamp(start, 0, document.GetPlainText().Length);
        length = Math.Clamp(length, 0, document.GetPlainText().Length - start);
        if (length == 0)
        {
            return;
        }

        var updated = document.SetStyle(start, length, style, enabled);
        var scrollPosition = CaptureEditorScrollPosition();
        SetEditorDocument(updated, start, length);
        RestoreEditorScrollPosition(scrollPosition);
        SetSaveStatus(_updateDocument(_noteId, updated));
        _lastAcceptedDocument = updated.Copy();
        UpdateToolbar();
    }

    private void RestoreHistory(bool undo)
    {
        var (start, length) = GetSelectionOffsets(ReadEditorDocument());
        var result = _editHistory(_noteId, undo);
        if (result.Document is null)
        {
            return;
        }

        SetEditorDocument(result.Document, start, length);
        _lastAcceptedDocument = result.Document.Copy();
        SetSaveStatus(result.Error);
        UpdateToolbar();
    }

    private void ApplyListKind(NoteBlockKind kind)
    {
        var document = ReadEditorDocument();
        var (start, length) = GetSelectionOffsets(document);
        var updated = document.ToggleListKind(start, length, kind);
        var scrollPosition = CaptureEditorScrollPosition();
        SetEditorDocument(updated, start, length);
        RestoreEditorScrollPosition(scrollPosition);
        SetSaveStatus(_updateDocument(_noteId, updated));
        _lastAcceptedDocument = updated.Copy();
        UpdateToolbar();
    }

    private void OnChecklistItemClick(Paragraph paragraph, bool isChecked)
    {
        try
        {
            var paragraphIndex = GetEditorParagraphs(Editor.Document).ToList().IndexOf(paragraph);
            var document = ReadEditorDocument().SetChecklistChecked(paragraphIndex, isChecked);
            _updatingEditor = true;
            try { ApplyChecklistCompletionAppearance(paragraph, isChecked); }
            finally { _updatingEditor = false; }
            SetSaveStatus(_updateDocument(_noteId, document));
            _lastAcceptedDocument = document.Copy();
            UpdateToolbar();
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
        }
    }

    private bool RemoveChecklistAtCaret()
    {
        if (!Editor.Selection.IsEmpty) return false;
        var document = ReadEditorDocument();
        var (start, _) = GetSelectionOffsets(document);
        var (index, offset) = document.GetParagraphPositionAt(start);
        if (offset != 0 || document.Paragraphs[index].Kind != NoteBlockKind.Checklist) return false;

        // Removing only the embedded checkbox leaves WPF's list boundaries for later Backspaces.
        ApplyListKind(NoteBlockKind.Checklist);
        return true;
    }

    private bool RemoveDividerAtCaret(bool backwards)
    {
        if (!Editor.Selection.IsEmpty) return false;
        var document = ReadEditorDocument();
        var (caret, _) = GetSelectionOffsets(document);
        var (index, offset) = document.GetParagraphPositionAt(caret);
        var paragraphLength = document.GetParagraphRange(index).Length;
        var dividerIndex = -1;
        if (document.Paragraphs[index].Kind == NoteBlockKind.Divider)
            dividerIndex = index;
        else if (backwards && offset == 0 && index > 0 &&
                 document.Paragraphs[index - 1].Kind == NoteBlockKind.Divider)
            dividerIndex = index - 1;
        else if (!backwards && offset == paragraphLength && index + 1 < document.Paragraphs.Count &&
                 document.Paragraphs[index + 1].Kind == NoteBlockKind.Divider)
            dividerIndex = index + 1;
        if (dividerIndex < 0) return false;

        var updated = document.Copy();
        var caretOffset = updated.GetParagraphRange(dividerIndex).Start;
        updated.Paragraphs.RemoveAt(dividerIndex);
        if (updated.Paragraphs.Count == 0) updated.Paragraphs.Add(new NoteParagraph());
        caretOffset = Math.Min(caretOffset, updated.GetPlainText().Length);
        SetEditorDocument(updated, caretOffset, 0);
        SetSaveStatus(_updateDocument(_noteId, updated));
        _lastAcceptedDocument = updated.Copy();
        UpdateToolbar();
        return true;
    }

    private bool ContinueListAtCaret()
    {
        var document = ReadEditorDocument();
        var (start, _) = GetSelectionOffsets(document);
        var paragraphIndex = document.GetParagraphIndexAt(start);
        if (document.Paragraphs[paragraphIndex].Kind is not (NoteBlockKind.Checklist or NoteBlockKind.Numbered))
        {
            return false;
        }

        var (paragraphStart, paragraphLength) = document.GetParagraphRange(paragraphIndex);
        var localOffset = Math.Clamp(start - paragraphStart, 0, paragraphLength);
        NotesDocument updated;
        int caretOffset;
        if (paragraphLength == 0)
        {
            updated = document.EndEmptyListItem(paragraphIndex);
            caretOffset = paragraphStart;
        }
        else
        {
            updated = document.SplitParagraph(paragraphIndex, localOffset);
            caretOffset = paragraphStart + localOffset + 2;
        }

        SetEditorDocument(updated, caretOffset, 0);
        SetSaveStatus(_updateDocument(_noteId, updated));
        _lastAcceptedDocument = updated.Copy();
        UpdateToolbar();
        return true;
    }

    private void OnGeometryChanged(object? sender, EventArgs e)
    {
        if (_initializing || _applyingRecoveredBounds || _isTransitioning || _isUserMoveResizeActive || !IsVisible || WindowState != WindowState.Normal)
        {
            return;
        }

        SaveCurrentBounds();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        ApplyPendingMarkupFormatting();
        if (_closingForApplicationExit)
        {
            CancelPositionTransition(restoreRestingPosition: false);
            return;
        }

        var result = _requestClose(_noteId);
        if (result.Allowed)
        {
            CancelPositionTransition(restoreRestingPosition: false);
            return;
        }

        e.Cancel = true;
        SetSaveStatus(result.Error ?? "The note could not be saved, so it remains open.");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowSource = PresentationSource.FromVisual(this) as HwndSource;
        _windowSource?.AddHook(WindowMessageHook);
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelPositionTransition(restoreRestingPosition: false);
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        base.OnClosed(e);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmEnterSizeMove = 0x0231;
        const int wmExitSizeMove = 0x0232;
        if (message == wmEnterSizeMove && _isTransitioning)
        {
            _moveResizeStartLeft = Left;
            _moveResizeStartTop = Top;
            _moveResizeRestingBounds = _restingBounds;
            _moveResizeTransitionCompletion = _transitionCompletion;
            _isUserMoveResizeActive = true;
            CancelPositionTransition(restoreRestingPosition: false, preserveCurrentPosition: true);
        }
        else if (message == wmExitSizeMove && _isUserMoveResizeActive)
        {
            var moved = Math.Abs(Left - _moveResizeStartLeft) > 1 || Math.Abs(Top - _moveResizeStartTop) > 1;
            if (!moved && _moveResizeRestingBounds is { } resting)
            {
                Left = resting.Left;
                Top = resting.Top;
            }

            _isUserMoveResizeActive = false;
            _moveResizeRestingBounds = null;
            SaveCurrentBounds();
            var finish = _moveResizeTransitionCompletion;
            _moveResizeTransitionCompletion = null;
            finish?.Invoke();
        }

        return IntPtr.Zero;
    }

    private void SaveCurrentBounds()
    {
        var bounds = new NoteBounds(Left, Top, ActualWidth, ActualHeight);
        try
        {
            SetSaveStatus(_updateBounds(_noteId, bounds));
            _restingBounds = bounds;
        }
        catch (InvalidDataException exception)
        {
            SetSaveStatus(exception.Message);
        }
    }

    private static DoubleAnimation CreatePositionAnimation(double from, double to, TimeSpan duration, bool showing) => new(from, to, duration)
    {
        EasingFunction = new QuadraticEase { EasingMode = showing ? EasingMode.EaseOut : EasingMode.EaseIn }
    };

    private void CancelPositionTransition(bool restoreRestingPosition, bool preserveCurrentPosition = false)
    {
        var currentLeft = Left;
        var currentTop = Top;
        _transitionRevision++;
        _transitionCompletion = null;
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        if (restoreRestingPosition)
        {
            Left = _restingBounds.Left;
            Top = _restingBounds.Top;
        }
        else if (preserveCurrentPosition)
        {
            Left = currentLeft;
            Top = currentTop;
        }

        _isTransitioning = false;
    }

    private void SetEditorDocument(NotesDocument document, int? selectionStart, int? selectionLength)
    {
        _activeMathSource = null;
        _inlineMathCommitPending = false;
        _pendingPlainUrl = null;
        _completedPlainUrl = null;
        _markupFormattingPending = false;
        _markupOriginalText = null;
        _editorRecoveryPending = false;
        _tablePasteRequested = false;
        _blockSyntaxPasteRequested = false;
        _updatingEditor = true;
        var flowDocument = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = Editor.FontFamily,
            FontSize = Editor.FontSize,
            LineHeight = GetEditorLineHeight(),
            LineStackingStrategy = GetEditorLineStackingStrategy()
        };
        AddEditorBlocks(flowDocument.Blocks, document.Paragraphs, allowTables: true);
        Editor.Document = flowDocument;
        UpdateTableWidths();
        if (selectionStart.HasValue)
        {
            var textLength = document.GetPlainText().Length;
            var startOffset = Math.Clamp(selectionStart.Value, 0, textLength);
            var endOffset = Math.Clamp(selectionStart.Value + selectionLength.GetValueOrDefault(), startOffset, textLength);
            var start = FindTextPosition(flowDocument, document, startOffset);
            var end = FindTextPosition(flowDocument, document, endOffset);
            Editor.Selection.Select(start, end);
        }

        _updatingEditor = false;
    }

    private double GetEditorLineHeight(double? fontSize = null) => _appearanceSettings.LineSpacing == 2
        ? double.NaN
        : (fontSize ?? Editor.FontSize) * (Editor.FontFamily.LineSpacing + (_appearanceSettings.LineSpacing - 2) / 4);

    private LineStackingStrategy GetEditorLineStackingStrategy() => _appearanceSettings.LineSpacing < 2
        ? LineStackingStrategy.BlockLineHeight
        : LineStackingStrategy.MaxHeight;

    private void AddEditorBlocks(BlockCollection blocks, IReadOnlyList<NoteParagraph> paragraphs, bool allowTables = false)
    {
        for (var index = 0; index < paragraphs.Count;)
        {
            var noteParagraph = paragraphs[index];
            if (noteParagraph.Cell is { } cell && allowTables)
            {
                var rows = paragraphs.Skip(index).TakeWhile(row => row.Cell?.TableId == cell.TableId).ToArray();
                blocks.Add(CreateEditorTable(rows));
                index += rows.Length;
                continue;
            }
            if (noteParagraph.Kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist)
            {
                var listKind = noteParagraph.Kind;
                var list = new System.Windows.Documents.List
                {
                    Tag = listKind,
                    MarkerStyle = listKind == NoteBlockKind.Bulleted ? TextMarkerStyle.Disc : TextMarkerStyle.None,
                    MarkerOffset = 18,
                    Padding = new Thickness(GetListMarkerWidth(), 0, 0, 0)
                };
                if (noteParagraph.Cell is not null) list.Margin = new Thickness(0);
                var number = 1;
                while (index < paragraphs.Count && paragraphs[index].Kind == listKind && paragraphs[index].Cell == noteParagraph.Cell)
                {
                    list.ListItems.Add(new ListItem(CreateEditorParagraph(paragraphs[index], number++)));
                    index++;
                }

                blocks.Add(list);
                foreach (var paragraph in list.ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>()))
                    ApplyInlineMarkerSpacing(paragraph);
                continue;
            }

            blocks.Add(CreateEditorParagraph(noteParagraph));
            index++;
        }

    }

    private NotesDocument ReadEditorDocument()
    {
        var paragraphs = new List<NoteParagraph>();
        ReadEditorBlocks(Editor.Document.Blocks, paragraphs);
        if (paragraphs.Count == 0) paragraphs.Add(new NoteParagraph());
        return new NotesDocument { Paragraphs = paragraphs };
    }

    private void ReadEditorBlocks(BlockCollection blocks, List<NoteParagraph> paragraphs, NoteTableCell? cell = null)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph paragraph)
            {
                var runs = new List<NoteTextRun>();
                ReadInlines(paragraph.Inlines, NoteTextStyle.None, runs);
                var kind = paragraph.Tag is NoteBlockKind blockKind ? blockKind : NoteBlockKind.Paragraph;
                if (kind == NoteBlockKind.Code)
                    runs = runs.Select(run => run with { Style = run.Style & ~NoteTextStyle.Code }).ToList();
                else if (kind == NoteBlockKind.Divider && runs.Any(run => run.Text.Length > 0))
                {
                    paragraph.Tag = null;
                    paragraph.ClearValue(Block.BorderBrushProperty);
                    paragraph.ClearValue(Block.BorderThicknessProperty);
                    paragraph.Margin = new Thickness(0, 0, 0, 8);
                    kind = NoteBlockKind.Paragraph;
                }
                paragraphs.Add(new NoteParagraph { Cell = cell, Kind = kind, Runs = runs });
            }
            else if (block is System.Windows.Documents.List list)
            {
                var kind = list.Tag is NoteBlockKind listKind ? listKind : list.MarkerStyle switch
                {
                    TextMarkerStyle.Decimal => NoteBlockKind.Numbered,
                    TextMarkerStyle.None => NoteBlockKind.Checklist,
                    _ => NoteBlockKind.Bulleted
                };
                foreach (var item in list.ListItems)
                {
                    foreach (var itemBlock in item.Blocks)
                    {
                        if (itemBlock is Paragraph itemParagraph)
                        {
                            var runs = new List<NoteTextRun>();
                            ReadInlines(itemParagraph.Inlines, NoteTextStyle.None, runs);
                            var isChecked = kind == NoteBlockKind.Checklist && itemParagraph.Inlines
                                .OfType<InlineUIContainer>()
                                .Select(container => container.Child)
                                .OfType<System.Windows.Controls.CheckBox>()
                                .Any(checkBox => checkBox.IsChecked == true);
                            paragraphs.Add(new NoteParagraph { Cell = cell, Kind = kind, IsChecked = isChecked, Runs = runs });
                        }
                        else
                        {
                            var text = new TextRange(itemBlock.ContentStart, itemBlock.ContentEnd).Text;
                            paragraphs.AddRange(NotesDocument.FromText(text).Paragraphs.Select(noteParagraph =>
                                new NoteParagraph { Cell = cell, Kind = kind, Runs = noteParagraph.Runs }));
                        }
                    }
                }
            }
            else if (block is Table table)
            {
                if (cell is not null) throw new InvalidDataException("Nested note tables are unsupported.");
                var id = table.Tag is Guid identity ? identity : Guid.NewGuid();
                table.Tag = id;
                var rowIndex = 0;
                foreach (var row in table.RowGroups.SelectMany(group => group.Rows))
                {
                    for (var column = 0; column < row.Cells.Count; column++)
                    {
                        var source = row.Cells[column];
                        if (source.RowSpan != 1 || source.ColumnSpan != 1)
                            throw new InvalidDataException("Merged note cells are unsupported.");
                        var alignment = source.TextAlignment switch
                        { TextAlignment.Center => NoteCellAlignment.Center, TextAlignment.Right => NoteCellAlignment.Right, _ => NoteCellAlignment.Left };
                        var position = new NoteTableCell(id, rowIndex, column, alignment);
                        var before = paragraphs.Count;
                        ReadEditorBlocks(source.Blocks, paragraphs, position);
                        if (paragraphs.Count == before) paragraphs.Add(new NoteParagraph { Cell = position });
                    }
                    rowIndex++;
                }
            }
            else
            {
                var text = new TextRange(block.ContentStart, block.ContentEnd).Text;
                paragraphs.AddRange(NotesDocument.FromText(text).Paragraphs.Select(row => { row.Cell = cell; return row; }));
            }
        }

    }

    private Paragraph CreateEditorParagraph(NoteParagraph noteParagraph, int number = 1)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4 * _appearanceSettings.LineSpacing) };
        var headingLevel = GetHeadingLevel(noteParagraph.Kind);
        if (headingLevel > 0)
        {
            paragraph.Tag = noteParagraph.Kind;
            paragraph.FontSize = GetHeadingFontSize(noteParagraph.Kind);
            // Fixed body-text line heights can push larger heading glyphs above the viewport.
            paragraph.LineStackingStrategy = LineStackingStrategy.MaxHeight;
            paragraph.LineHeight = GetEditorLineHeight(paragraph.FontSize);
        }
        else if (noteParagraph.Kind == NoteBlockKind.Divider)
        {
            paragraph.Tag = NoteBlockKind.Divider;
            paragraph.Margin = new Thickness(0, 4, 0, 8);
            paragraph.BorderThickness = new Thickness(0, 0, 0, 1);
            paragraph.SetResourceReference(Block.BorderBrushProperty, "NotesContentBorderBrush");
        }
        else if (noteParagraph.Kind == NoteBlockKind.DisplayMath)
        {
            paragraph.Tag = NoteBlockKind.DisplayMath;
            paragraph.TextAlignment = TextAlignment.Center;
            paragraph.Margin = new Thickness(0, 4, 0, 10);
        }
        if (noteParagraph.Kind == NoteBlockKind.Code)
        {
            paragraph.Tag = NoteBlockKind.Code;
            paragraph.FontFamily = CodeFont;
            paragraph.SetResourceReference(Block.PaddingProperty, "NotesCodePadding");
            paragraph.BorderThickness = new Thickness(1);
            paragraph.SetResourceReference(Block.BorderBrushProperty, "NotesContentBorderBrush");
            paragraph.SetResourceReference(TextElement.BackgroundProperty, "NotesCodeBrush");
        }
        if (noteParagraph.Kind == NoteBlockKind.Numbered)
        {
            paragraph.Inlines.Add(new InlineUIContainer(new TextBlock
            {
                Text = $"{number}.", IsHitTestVisible = false
            }) { BaselineAlignment = BaselineAlignment.Baseline });
        }
        if (noteParagraph.Kind == NoteBlockKind.Checklist)
        {
            var text = string.Concat(noteParagraph.Runs.Select(run => run.Text));
            var checkBox = new System.Windows.Controls.CheckBox
            {
                Style = (Style)FindResource("ChecklistCheckbox"),
                IsChecked = noteParagraph.IsChecked,
                Focusable = true,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = noteParagraph.IsChecked ? "Mark task incomplete" : "Mark task complete"
            };
            AutomationProperties.SetName(checkBox, text.Length == 0 ? "Checklist item" : $"Checklist item: {text}");
            AutomationProperties.SetHelpText(checkBox, noteParagraph.IsChecked
                ? "Checked. Press Space to mark incomplete."
                : "Not checked. Press Space to mark complete.");
            checkBox.Click += (_, _) => OnChecklistItemClick(paragraph, checkBox.IsChecked == true);
            paragraph.Inlines.Add(new InlineUIContainer(checkBox) { BaselineAlignment = BaselineAlignment.Center });
        }

        if (noteParagraph.Kind == NoteBlockKind.DisplayMath)
        {
            paragraph.Inlines.Add(CreateDisplayMath(new NoteTextRun(string.Concat(noteParagraph.Runs.Select(run => run.Text)))));
            return paragraph;
        }

        for (var runIndex = 0; runIndex < noteParagraph.Runs.Count;)
        {
            var noteRun = noteParagraph.Runs[runIndex];
            if (noteRun.Kind == NoteInlineKind.InlineMath)
            {
                paragraph.Inlines.Add(CreateInlineMath(noteRun));
                runIndex++;
                continue;
            }
            if (noteRun.Kind == NoteInlineKind.Link)
            {
                var hyperlink = CreateLink(noteRun.LinkDestination!);
                do
                {
                    AddStyledText(hyperlink.Inlines, noteParagraph.Runs[runIndex], headingLevel > 0);
                    runIndex++;
                }
                while (runIndex < noteParagraph.Runs.Count &&
                       noteParagraph.Runs[runIndex].Kind == NoteInlineKind.Link &&
                       noteParagraph.Runs[runIndex].LinkDestination == noteRun.LinkDestination);
                paragraph.Inlines.Add(hyperlink);
                continue;
            }
            AddStyledText(paragraph.Inlines, noteRun, headingLevel > 0);
            runIndex++;
        }

        if (noteParagraph.Kind == NoteBlockKind.Checklist)
            ApplyChecklistCompletionAppearance(paragraph, noteParagraph.IsChecked);
        return paragraph;
    }

    private static void ApplyChecklistCompletionAppearance(Paragraph paragraph, bool isChecked)
    {
        paragraph.TextDecorations = isChecked ? TextDecorations.Strikethrough : new();
        if (isChecked)
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "NotesSecondaryTextBrush");
        else
            paragraph.ClearValue(TextElement.ForegroundProperty);
    }

    private void RefreshNumberedMarkers()
    {
        // Inline edits invalidate WPF block enumerators, so capture the targets first.
        foreach (var list in GetEditorBlocks(Editor.Document.Blocks).OfType<System.Windows.Documents.List>()
                     .Where(list => list.Tag is NoteBlockKind.Numbered).ToArray())
        {
            var index = 1;
            foreach (var paragraph in list.ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>()).ToArray())
            {
                var markers = paragraph.Inlines.OfType<InlineUIContainer>().ToArray();
                var label = markers.FirstOrDefault()?.Child as TextBlock;
                if (label is null)
                {
                    label = new TextBlock { IsHitTestVisible = false };
                    var container = new InlineUIContainer(label) { BaselineAlignment = BaselineAlignment.Baseline };
                    if (paragraph.Inlines.FirstInline is { } first)
                        paragraph.Inlines.InsertBefore(first, container);
                    else
                        paragraph.Inlines.Add(container);
                }
                foreach (var extra in markers.Skip(1))
                    paragraph.Inlines.Remove(extra);
                label.Text = $"{index++}.";
                ApplyInlineMarkerSpacing(paragraph);
            }
        }
    }

    private void ApplyInlineMarkerSpacing(Paragraph paragraph)
    {
        if (paragraph.Inlines.FirstInline is not InlineUIContainer { Child: FrameworkElement marker })
            return;

        // Match the native disc marker's left edge while keeping wrapped text aligned.
        var markerWidth = GetListMarkerWidth();
        paragraph.TextIndent = -markerWidth;
        if (marker is System.Windows.Controls.CheckBox)
            marker.Margin = new Thickness(0, 0, markerWidth - marker.Width, 0);
        else if (marker is TextBlock number)
        {
            var list = (System.Windows.Documents.List)((ListItem)paragraph.Parent).Parent;
            var typeface = new Typeface(Editor.FontFamily, Editor.FontStyle, Editor.FontWeight, Editor.FontStretch);
            var label = Measure(number.Text, typeface);
            var widest = Measure($"{list.ListItems.Count}.", typeface);
            number.Width = Math.Max(markerWidth, widest.Width + 6);
            var bearing = label.BuildGeometry(new System.Windows.Point()).Bounds.Left;
            number.Margin = new Thickness(-bearing, 0, bearing, 0);
            paragraph.Margin = new Thickness(number.Width - markerWidth, 0, 0, 4 * _appearanceSettings.LineSpacing);
            paragraph.TextIndent = -number.Width;
        }

        FormattedText Measure(string text, Typeface typeface) => new(text,
            System.Globalization.CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
            typeface, Editor.FontSize, Editor.Foreground, VisualTreeHelper.GetDpi(Editor).PixelsPerDip);
    }

    private double GetListMarkerWidth()
    {
        var bullet = new FormattedText("\u009f", System.Globalization.CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight, new Typeface("Wingdings"), Editor.FontSize,
            Editor.Foreground, VisualTreeHelper.GetDpi(Editor).PixelsPerDip);
        return 18 + bullet.Width - bullet.BuildGeometry(new System.Windows.Point()).Bounds.Left;
    }

    private static void ReadInlines(InlineCollection inlines, NoteTextStyle inheritedStyle, List<NoteTextRun> runs,
        string? linkDestination = null, bool linkUnderline = false, bool manualLinkUnderline = false)
    {
        foreach (var inline in inlines)
        {
            var style = ReadStyle(inline, inheritedStyle);
            var localDecorations = inline.ReadLocalValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
            var localUnderline = localDecorations?.Any(decoration =>
                decoration.Location == TextDecorationLocation.Underline) == true;
            var preserveLinkUnderline = manualLinkUnderline || localUnderline;
            if (linkDestination is not null && linkUnderline && !preserveLinkUnderline)
                style &= ~NoteTextStyle.Underline;
            switch (inline)
            {
                case Run run:
                    AddRun(runs, run.Text, style, linkDestination is null ? NoteInlineKind.Text : NoteInlineKind.Link,
                        linkDestination);
                    break;
                case LineBreak:
                    AddRun(runs, "\n", style,
                        linkDestination is null ? NoteInlineKind.Text : NoteInlineKind.Link, linkDestination);
                    break;
                case InlineUIContainer { Child: NoteInlineMathButton mathButton }:
                    AddRun(runs, mathButton.NoteRun.Text, mathButton.NoteRun.Style,
                        mathButton.IsDisplayMath ? NoteInlineKind.Text : NoteInlineKind.InlineMath);
                    break;
                case NoteInlineMathSourceSpan mathSource:
                    var sourceRuns = new List<NoteTextRun>();
                    ReadInlines(mathSource.Inlines, style, sourceRuns);
                    AddRun(runs, string.Concat(sourceRuns.Select(run => run.Text)), style,
                        mathSource.IsDisplayMath ? NoteInlineKind.Text : NoteInlineKind.InlineMath);
                    break;
                case InlineUIContainer:
                    break;
                case Hyperlink hyperlink:
                    var childStyle = style & ~NoteTextStyle.Underline;
                    ReadInlines(hyperlink.Inlines, childStyle, runs, hyperlink.NavigateUri?.OriginalString,
                        linkUnderline: true);
                    break;
                case Span span:
                    ReadInlines(span.Inlines, style, runs, linkDestination, linkUnderline, preserveLinkUnderline);
                    break;
                default:
                    try
                    {
                        AddRun(runs, new TextRange(inline.ContentStart, inline.ContentEnd).Text, style,
                            linkDestination is null ? NoteInlineKind.Text : NoteInlineKind.Link, linkDestination);
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    break;
            }
        }
    }

    private static NoteTextStyle ReadStyle(Inline inline, NoteTextStyle inheritedStyle)
    {
        var style = inline.FontWeight == FontWeights.Bold
            ? inheritedStyle | NoteTextStyle.Bold
            : inheritedStyle & ~NoteTextStyle.Bold;
        style = inline.FontStyle == FontStyles.Italic ? style | NoteTextStyle.Italic : style & ~NoteTextStyle.Italic;
        style = inline.FontFamily.Source == CodeFont.Source ? style | NoteTextStyle.Code : style & ~NoteTextStyle.Code;
        foreach (var decoration in inline.TextDecorations)
        {
            if (decoration.Location == TextDecorationLocation.Underline)
            {
                style |= NoteTextStyle.Underline;
            }
            else if (decoration.Location == TextDecorationLocation.Strikethrough)
            {
                style |= NoteTextStyle.Strikethrough;
            }
        }

        return style;
    }

    private static void AddRun(List<NoteTextRun> runs, string text, NoteTextStyle style,
        NoteInlineKind kind = NoteInlineKind.Text, string? linkDestination = null)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (runs.Count > 0 && runs[^1].Style == style && runs[^1].Kind == kind &&
            runs[^1].LinkDestination == linkDestination)
        {
            runs[^1] = runs[^1] with { Text = runs[^1].Text + text };
        }
        else
        {
            runs.Add(new NoteTextRun(text, style, kind, linkDestination));
        }
    }

    private static void AddStyledText(InlineCollection inlines, NoteTextRun noteRun, bool heading = false)
    {
        var normalized = noteRun.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var segments = normalized.Split('\n');
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length > 0)
            {
                var run = new Run(segments[index])
                {
                    FontWeight = noteRun.Style.HasFlag(NoteTextStyle.Bold) ? FontWeights.Bold : heading ? FontWeights.SemiBold : FontWeights.Normal,
                    FontStyle = noteRun.Style.HasFlag(NoteTextStyle.Italic) ? FontStyles.Italic : FontStyles.Normal
                };
                if (noteRun.Kind != NoteInlineKind.Link || noteRun.Style.HasFlag(NoteTextStyle.Underline))
                    run.TextDecorations = CreateDecorations(noteRun.Style);
                if (noteRun.Style.HasFlag(NoteTextStyle.Code))
                {
                    run.FontFamily = CodeFont;
                    run.SetResourceReference(TextElement.BackgroundProperty, "NotesInlineCodeBrush");
                }
                inlines.Add(run);
            }

            if (index < segments.Length - 1)
            {
                inlines.Add(new LineBreak());
            }
        }
    }

    private static int GetHeadingLevel(NoteBlockKind kind) => kind switch
    {
        NoteBlockKind.Heading1 => 1,
        NoteBlockKind.Heading2 => 2,
        NoteBlockKind.Heading3 => 3,
        NoteBlockKind.Heading4 => 4,
        NoteBlockKind.Heading5 => 5,
        NoteBlockKind.Heading6 => 6,
        _ => 0
    };

    private double GetHeadingFontSize(NoteBlockKind kind) => Editor.FontSize * (GetHeadingLevel(kind) switch
    {
        1 => 2.0,
        2 => 1.7,
        3 => 1.45,
        4 => 1.25,
        5 => 1.1,
        _ => 1.0
    });
    private static TextDecorationCollection CreateDecorations(NoteTextStyle style)
    {
        var decorations = new TextDecorationCollection();
        if (style.HasFlag(NoteTextStyle.Underline))
        {
            decorations.Add(TextDecorations.Underline[0].Clone());
        }

        if (style.HasFlag(NoteTextStyle.Strikethrough))
        {
            decorations.Add(TextDecorations.Strikethrough[0].Clone());
        }

        return decorations;
    }

    private static TextPointer FindTextPosition(FlowDocument document, NotesDocument noteDocument, int targetOffset)
    {
        var (paragraphIndex, paragraphOffset) = noteDocument.GetParagraphPositionAt(targetOffset);
        var paragraph = GetEditorParagraphs(document).ElementAt(paragraphIndex);
        var offset = 0;
        foreach (var inline in Flatten(paragraph.Inlines))
        {
            if (inline is Run run)
            {
                var length = NormalizeLineBreaks(run.Text).Length;
                if (paragraphOffset <= offset + length)
                {
                    var runOffset = Math.Clamp(paragraphOffset - offset, 0, length);
                    return run.ContentStart.GetPositionAtOffset(RawOffset(run.Text, runOffset), LogicalDirection.Forward)
                        ?? run.ContentStart;
                }

                offset += length;
            }
            else if (inline is LineBreak lineBreak)
            {
                if (paragraphOffset <= offset + 2)
                {
                    return paragraphOffset == offset ? lineBreak.ContentStart : lineBreak.ContentEnd;
                }

                offset += 2;
            }
            else if (inline is InlineUIContainer { Child: NoteInlineMathButton math })
            {
                var length = NormalizeLineBreaks(math.NoteRun.Text).Length;
                if (paragraphOffset <= offset + length)
                    return paragraphOffset == offset ? inline.ContentStart : inline.ContentEnd;
                offset += length;
            }
        }

        return paragraph.ContentEnd;
    }

    private static IEnumerable<Paragraph> GetEditorParagraphs(FlowDocument document)
    {
        return GetEditorBlocks(document.Blocks).OfType<Paragraph>();
    }

    private static IEnumerable<Block> GetEditorBlocks(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block is System.Windows.Documents.List list)
            {
                foreach (var item in list.ListItems)
                    foreach (var nested in GetEditorBlocks(item.Blocks)) yield return nested;
            }
            else if (block is Table table)
                foreach (var cell in table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells))
                    foreach (var nested in GetEditorBlocks(cell.Blocks)) yield return nested;
        }
    }

    private static IEnumerable<Inline> Flatten(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Span span)
            {
                foreach (var nested in Flatten(span.Inlines))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return inline;
            }
        }
    }

    private static bool? HasProperty(TextRange selection, DependencyProperty property, object expected)
    {
        var value = selection.GetPropertyValue(property);
        return ReferenceEquals(value, DependencyProperty.UnsetValue) ? null : Equals(value, expected);
    }

    private void UpdateToolbar()
    {
        if (Editor is null)
        {
            return;
        }

        var selection = Editor.Selection;
        BoldButton.IsChecked = HasProperty(selection, TextElement.FontWeightProperty, FontWeights.Bold);
        ItalicButton.IsChecked = HasProperty(selection, TextElement.FontStyleProperty, FontStyles.Italic);
        UnderlineButton.IsChecked = HasDecoration(selection, TextDecorationLocation.Underline);
        StrikethroughButton.IsChecked = HasDecoration(selection, TextDecorationLocation.Strikethrough);

        var document = ReadEditorDocument();
        var (start, length) = GetSelectionOffsets(document);
        var listKind = document.GetListKind(start, length);
        BulletsButton.IsChecked = listKind.HasValue ? listKind == NoteBlockKind.Bulleted : null;
        NumberedButton.IsChecked = listKind.HasValue ? listKind == NoteBlockKind.Numbered : null;
        ChecklistButton.IsChecked = listKind.HasValue ? listKind == NoteBlockKind.Checklist : null;
    }

    private (int Start, int Length) GetSelectionOffsets(NotesDocument document)
    {
        var textLength = document.GetPlainText().Length;
        var start = Math.Clamp(GetTextOffset(Editor.Selection.Start), 0, textLength);
        var end = Math.Clamp(GetTextOffset(Editor.Selection.End), start, textLength);
        return (start, end - start);

        // TextRange.Text adds list markers and spaces for embedded controls. Count only stored text.
        int GetTextOffset(TextPointer position)
        {
            var offset = 0;
            foreach (var paragraph in GetEditorParagraphs(Editor.Document))
            {
                if (position.CompareTo(paragraph.ContentStart) <= 0) return offset;
                foreach (var inline in Flatten(paragraph.Inlines))
                {
                    if (position.CompareTo(inline.ContentStart) <= 0) return offset;
                    if (inline is Run run)
                    {
                        var count = position.CompareTo(run.ContentEnd) >= 0
                            ? run.Text.Length
                            : Math.Clamp(run.ContentStart.GetOffsetToPosition(position), 0, run.Text.Length);
                        offset += NormalizeLineBreaks(run.Text[..count]).Length;
                    }
                    else if (inline is LineBreak && position.CompareTo(inline.ContentEnd) >= 0)
                    {
                        offset += 2;
                    }
                    else if (inline is InlineUIContainer { Child: NoteInlineMathButton math } &&
                             position.CompareTo(inline.ContentEnd) >= 0)
                    {
                        offset += NormalizeLineBreaks(math.NoteRun.Text).Length;
                    }

                    if (position.CompareTo(inline.ContentEnd) <= 0) return offset;
                }

                if (position.CompareTo(paragraph.ContentEnd) <= 0) return offset;
                offset += 2;
            }

            return offset;
        }
    }

    private static SolidColorBrush CreateBrush(string argb) => new(System.Windows.Media.Color.FromArgb(
        Convert.ToByte(argb.Substring(1, 2), 16),
        Convert.ToByte(argb.Substring(3, 2), 16),
        Convert.ToByte(argb.Substring(5, 2), 16),
        Convert.ToByte(argb.Substring(7, 2), 16)));

    private static bool? HasDecoration(TextRange selection, TextDecorationLocation location)
    {
        var value = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        if (ReferenceEquals(value, DependencyProperty.UnsetValue))
        {
            return null;
        }

        return value is TextDecorationCollection collection && collection.Any(decoration => decoration.Location == location);
    }

    private void OnHeaderMenuClick(object sender, RoutedEventArgs e)
    {
        MenuButton.ContextMenu!.PlacementTarget = MenuButton;
        MenuButton.ContextMenu.IsOpen = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnNewNoteClick(object sender, RoutedEventArgs e) => _createNote();

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
            e.Handled = true;
        }
    }

    private System.Windows.Controls.ContextMenu CreateHeaderMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        NotesAppearance.Apply(menu, _appearanceSettings.Theme, _uiStyle);
        var noteColors = new System.Windows.Controls.MenuItem { Header = "Note _color" };
        foreach (var color in NoteColorPalette.Colors)
        {
            var colorItem = new System.Windows.Controls.MenuItem
            {
                Header = color.ToString(),
                IsCheckable = true,
                IsChecked = color == _noteColor
            };
            colorItem.Click += (_, _) => OnColorClick(color);
            noteColors.Items.Add(colorItem);
        }
        var notesList = new System.Windows.Controls.MenuItem { Header = "_Notes list" };
        notesList.Click += (_, _) => _showNotesList();
        var deleteNote = new System.Windows.Controls.MenuItem { Header = "_Delete note" };
        deleteNote.Click += (_, _) => OnDeleteNoteClick();
        var notesSettings = new System.Windows.Controls.MenuItem { Header = "Notes _settings" };
        notesSettings.Click += (_, _) => _showNotesSettings();
        menu.Items.Add(noteColors);
        menu.Items.Add(notesList);
        menu.Items.Add(deleteNote);
        menu.Items.Add(notesSettings);
        return menu;
    }
}
