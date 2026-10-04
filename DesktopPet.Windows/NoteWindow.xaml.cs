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
    private bool _closingForApplicationExit;

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
        string uiStyle = UiStyleCatalog.StickyNotes)
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
        _appearanceSettings = appearanceSettings;
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        _noteColor = note.Color;
        _restingBounds = note.Bounds;
        _lastAcceptedDocument = note.Document.Copy();
        InitializeComponent();
        NoteTitle.Text = note.DisplayName;
        Title = note.DisplayName;
        Left = note.Bounds.Left;
        Top = note.Bounds.Top;
        Width = note.Bounds.Width;
        Height = note.Bounds.Height;
        ApplyAppearance(appearanceSettings, note.Color, _uiStyle);
        SetEditorDocument(note.Document, null, null);
        MenuButton.ContextMenu = CreateHeaderMenu();
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
        var previousSettings = _appearanceSettings;
        var scrollPosition = CaptureEditorScrollPosition();
        _appearanceSettings = settings;
        _noteColor = color;
        if (uiStyle is not null)
        {
            _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        }

        var darkTheme = NotesAppearance.Apply(this, settings.Theme, _uiStyle);
        RestoreEditorScrollPosition(scrollPosition);
        if (MenuButton.ContextMenu is { } menu)
        {
            NotesAppearance.Apply(menu, settings.Theme, _uiStyle);
        }
        Background = CreateBrush(NoteColorPalette.GetBackgroundHex(color, darkTheme, _uiStyle));
        var foreground = CreateBrush(NoteColorPalette.GetForegroundHex(darkTheme));
        Foreground = foreground;
        NoteTitle.Foreground = foreground;
        NoteTitle.CaretBrush = foreground;
        Editor.Foreground = foreground;
        Editor.CaretBrush = foreground;
        if (!_appearanceInitialized || previousSettings.TextSize != settings.TextSize)
        {
            Editor.FontSize = settings.TextSize;
            Editor.Document.FontSize = settings.TextSize;
            foreach (var list in Editor.Document.Blocks.OfType<System.Windows.Documents.List>())
                foreach (var paragraph in list.ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>()))
                    ApplyInlineMarkerSpacing(paragraph);
        }
        SaveStatus.Foreground = CreateBrush(darkTheme ? "#FFFFB4AB" : "#FF8B1F1F");
        if (!_appearanceInitialized || previousSettings.AlwaysOnTop != settings.AlwaysOnTop)
        {
            Topmost = settings.AlwaysOnTop;
        }
        _appearanceInitialized = true;
    }

    private (double Horizontal, double Vertical)? CaptureEditorScrollPosition() =>
        Editor.Template.FindName("PART_ContentHost", Editor) is System.Windows.Controls.ScrollViewer scrollViewer
            ? (scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset)
            : null;

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
        if (_initializing || _updatingEditor)
        {
            return;
        }

        try
        {
            var document = ReadEditorDocument();
            if (document.GetPlainText().Length > NotesSnapshotValidator.MaximumTextCharacters)
            {
                SetEditorDocument(_lastAcceptedDocument, null, null);
                SetSaveStatus($"A note is limited to {NotesSnapshotValidator.MaximumTextCharacters:N0} characters; the extra text was not accepted.");
                return;
            }

            SetSaveStatus(_updateDocument(_noteId, document));
            _lastAcceptedDocument = document.Copy();
            _updatingEditor = true;
            try { RefreshNumberedMarkers(); }
            finally { _updatingEditor = false; }
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetEditorDocument(_lastAcceptedDocument, null, null);
            SetSaveStatus(exception.Message);
        }
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e) => UpdateToolbar();

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

        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (control && !shift && e.Key == Key.Z)
        {
            RestoreHistory(undo: true);
            e.Handled = true;
        }
        else if (control && !shift && e.Key == Key.Y)
        {
            RestoreHistory(undo: false);
            e.Handled = true;
        }
        else if (control && shift && e.Key == Key.X)
        {
            ApplyStyle(NoteTextStyle.Strikethrough, StrikethroughButton.IsChecked != true);
            e.Handled = true;
        }
        else if (control && !shift && e.Key == Key.B)
        {
            ApplyStyle(NoteTextStyle.Bold, BoldButton.IsChecked != true);
            e.Handled = true;
        }
        else if (control && !shift && e.Key == Key.I)
        {
            ApplyStyle(NoteTextStyle.Italic, ItalicButton.IsChecked != true);
            e.Handled = true;
        }
        else if (control && !shift && e.Key == Key.U)
        {
            ApplyStyle(NoteTextStyle.Underline, UnderlineButton.IsChecked != true);
            e.Handled = true;
        }
        else if (!control && !shift && e.Key == Key.Enter && Editor.Selection.IsEmpty && ContinueListAtCaret())
        {
            e.Handled = true;
        }
    }

    private void ApplyStyle(NoteTextStyle style, bool enabled)
    {
        if (Editor.Selection.IsEmpty)
        {
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
        SetEditorDocument(updated, start, length);
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
        SetEditorDocument(updated, start, length);
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
            SetSaveStatus(_updateDocument(_noteId, document));
            _lastAcceptedDocument = document.Copy();
            UpdateToolbar();
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            SetSaveStatus(exception.Message);
        }
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
        _updatingEditor = true;
        var flowDocument = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = Editor.FontFamily,
            FontSize = Editor.FontSize
        };
        for (var index = 0; index < document.Paragraphs.Count;)
        {
            var noteParagraph = document.Paragraphs[index];
            if (noteParagraph.Kind is NoteBlockKind.Bulleted or NoteBlockKind.Numbered or NoteBlockKind.Checklist)
            {
                var listKind = noteParagraph.Kind;
                var list = new System.Windows.Documents.List
                {
                    Tag = listKind,
                    MarkerStyle = listKind == NoteBlockKind.Bulleted ? TextMarkerStyle.Disc : TextMarkerStyle.None,
                    MarkerOffset = 18
                };
                var number = 1;
                while (index < document.Paragraphs.Count && document.Paragraphs[index].Kind == listKind)
                {
                    list.ListItems.Add(new ListItem(CreateEditorParagraph(document.Paragraphs[index], number++)));
                    index++;
                }

                flowDocument.Blocks.Add(list);
                foreach (var paragraph in list.ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>()))
                    ApplyInlineMarkerSpacing(paragraph);
                continue;
            }

            flowDocument.Blocks.Add(CreateEditorParagraph(noteParagraph));
            index++;
        }

        Editor.Document = flowDocument;
        if (selectionStart.HasValue)
        {
            var start = FindTextPosition(flowDocument, document, selectionStart.Value);
            var end = FindTextPosition(flowDocument, document, selectionStart.Value + selectionLength.GetValueOrDefault());
            Editor.Selection.Select(start, end);
        }

        _updatingEditor = false;
    }

    private NotesDocument ReadEditorDocument()
    {
        var paragraphs = new List<NoteParagraph>();
        foreach (var block in Editor.Document.Blocks)
        {
            if (block is Paragraph paragraph)
            {
                var runs = new List<NoteTextRun>();
                ReadInlines(paragraph.Inlines, NoteTextStyle.None, runs);
                paragraphs.Add(new NoteParagraph { Runs = runs });
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
                            paragraphs.Add(new NoteParagraph { Kind = kind, IsChecked = isChecked, Runs = runs });
                        }
                        else
                        {
                            var text = new TextRange(itemBlock.ContentStart, itemBlock.ContentEnd).Text;
                            paragraphs.AddRange(NotesDocument.FromText(text).Paragraphs.Select(noteParagraph =>
                                new NoteParagraph { Kind = kind, Runs = noteParagraph.Runs }));
                        }
                    }
                }
            }
            else
            {
                var text = new TextRange(block.ContentStart, block.ContentEnd).Text;
                paragraphs.AddRange(NotesDocument.FromText(text).Paragraphs);
            }
        }

        if (paragraphs.Count == 0)
        {
            paragraphs.Add(new NoteParagraph());
        }

        return new NotesDocument { Paragraphs = paragraphs };
    }

    private Paragraph CreateEditorParagraph(NoteParagraph noteParagraph, int number = 1)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
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

        foreach (var noteRun in noteParagraph.Runs)
        {
            AddStyledText(paragraph, noteRun);
        }

        return paragraph;
    }

    private void RefreshNumberedMarkers()
    {
        foreach (var list in Editor.Document.Blocks.OfType<System.Windows.Documents.List>()
                     .Where(list => list.Tag is NoteBlockKind.Numbered))
        {
            var index = 1;
            foreach (var paragraph in list.ListItems.SelectMany(item => item.Blocks.OfType<Paragraph>()))
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
        var bullet = Measure("\u009f", new Typeface("Wingdings"));
        var markerWidth = 18 + bullet.Width - bullet.BuildGeometry(new System.Windows.Point()).Bounds.Left;
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
            paragraph.Margin = new Thickness(number.Width - markerWidth, 0, 0, 8);
            paragraph.TextIndent = -number.Width;
        }

        FormattedText Measure(string text, Typeface typeface) => new(text,
            System.Globalization.CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
            typeface, Editor.FontSize, Editor.Foreground, VisualTreeHelper.GetDpi(Editor).PixelsPerDip);
    }

    private static void ReadInlines(InlineCollection inlines, NoteTextStyle inheritedStyle, List<NoteTextRun> runs)
    {
        foreach (var inline in inlines)
        {
            var style = ReadStyle(inline, inheritedStyle);
            switch (inline)
            {
                case Run run:
                    AddRun(runs, run.Text, style);
                    break;
                case LineBreak:
                    AddRun(runs, "\n", style);
                    break;
                case InlineUIContainer:
                    break;
                case Span span:
                    ReadInlines(span.Inlines, style, runs);
                    break;
                default:
                    try
                    {
                        AddRun(runs, new TextRange(inline.ContentStart, inline.ContentEnd).Text, style);
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

    private static void AddRun(List<NoteTextRun> runs, string text, NoteTextStyle style)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (runs.Count > 0 && runs[^1].Style == style)
        {
            runs[^1] = runs[^1] with { Text = runs[^1].Text + text };
        }
        else
        {
            runs.Add(new NoteTextRun(text, style));
        }
    }

    private static void AddStyledText(Paragraph paragraph, NoteTextRun noteRun)
    {
        var normalized = noteRun.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var segments = normalized.Split('\n');
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length > 0)
            {
                var run = new Run(segments[index])
                {
                    FontWeight = noteRun.Style.HasFlag(NoteTextStyle.Bold) ? FontWeights.Bold : FontWeights.Normal,
                    FontStyle = noteRun.Style.HasFlag(NoteTextStyle.Italic) ? FontStyles.Italic : FontStyles.Normal,
                    TextDecorations = CreateDecorations(noteRun.Style)
                };
                paragraph.Inlines.Add(run);
            }

            if (index < segments.Length - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }
    }

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
        }

        return paragraph.ContentEnd;
    }

    private static IEnumerable<Paragraph> GetEditorParagraphs(FlowDocument document)
    {
        foreach (var block in document.Blocks)
        {
            if (block is Paragraph paragraph)
            {
                yield return paragraph;
            }
            else if (block is System.Windows.Documents.List list)
            {
                foreach (var item in list.ListItems)
                {
                    foreach (var itemBlock in item.Blocks.OfType<Paragraph>())
                    {
                        yield return itemBlock;
                    }
                }
            }
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
        var newNote = new System.Windows.Controls.MenuItem { Header = "_New note" };
        newNote.Click += (_, _) => _createNote();
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
        menu.Items.Add(newNote);
        menu.Items.Add(noteColors);
        menu.Items.Add(notesList);
        menu.Items.Add(deleteNote);
        menu.Items.Add(notesSettings);
        return menu;
    }
}
