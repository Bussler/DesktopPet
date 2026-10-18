using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using DesktopPet.Core;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;

namespace DesktopPet.Windows;

public partial class NoteWindow
{
    private (int Start, int Length)? _pendingPlainUrl;
    private (int Start, int Length)? _completedPlainUrl;
    private bool _plainUrlCommitPending;

    private void InitializeLinkEditing() => Editor.LostKeyboardFocus += OnEditorLostKeyboardFocus;

    private void RefreshLinkMenus()
    {
        foreach (var link in GetEditorParagraphs(Editor.Document)
                     .SelectMany(paragraph => GetLinks(paragraph.Inlines)))
            if (link.ContextMenu is { } menu)
                NotesAppearance.Apply(menu, _appearanceSettings.Theme, _uiStyle);
    }

    private static IEnumerable<Hyperlink> GetLinks(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Hyperlink link) yield return link;
            if (inline is Span span)
                foreach (var nested in GetLinks(span.Inlines)) yield return nested;
        }
    }
    private Hyperlink CreateLink(string destination)
    {
        var link = new Hyperlink
        {
            NavigateUri = new Uri(destination, UriKind.Absolute),
            Cursor = System.Windows.Input.Cursors.Hand,
            TextDecorations = TextDecorations.Underline
        };
        link.SetResourceReference(TextElement.ForegroundProperty, "NotesLinkBrush");
        link.MouseEnter += (_, _) => SetLinkFeedbackBrush(link, "AppAccentBrush");
        link.MouseLeave += (_, _) => SetLinkFeedbackBrush(link,
            link.IsKeyboardFocused ? "AppFocusBrush" : "NotesLinkBrush");
        link.GotKeyboardFocus += (_, _) => SetLinkFeedbackBrush(link, "AppFocusBrush");
        link.LostKeyboardFocus += (_, _) => SetLinkFeedbackBrush(link,
            link.IsMouseOver ? "AppAccentBrush" : "NotesLinkBrush");
        link.RequestNavigate += OnLinkRequestNavigate;
        link.ContextMenu = CreateLinkContextMenu(link);
        link.ToolTip = destination;
        return link;
    }

    private static void SetLinkFeedbackBrush(Hyperlink link, string resourceKey) =>
        link.SetResourceReference(TextElement.ForegroundProperty, resourceKey);

    private WpfContextMenu CreateLinkContextMenu(Hyperlink link)
    {
        var menu = new WpfContextMenu();
        NotesAppearance.Apply(menu, _appearanceSettings.Theme, _uiStyle);
        var edit = new WpfMenuItem { Header = "Edit link" };
        edit.Click += (_, _) => EditLink(link);
        var remove = new WpfMenuItem { Header = "Remove link" };
        remove.Click += (_, _) => RemoveLink(link);
        menu.Items.Add(edit);
        menu.Items.Add(remove);
        return menu;
    }

    private void OnLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        var destination = e.Uri?.OriginalString;
        if (!NoteLinkFormatting.IsValidDestination(destination))
        {
            ShowLinkFeedback("This saved link address is invalid.");
            return;
        }

        try
        {
            _openLink(destination!);
            ClearLinkFeedback();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or
                                          IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
        {
            ShowLinkFeedback($"The link could not be opened. {exception.Message}");
        }
    }

    private void EditLink(Hyperlink link)
    {
        var range = GetLinkRange(link);
        if (range is null) return;
        var destination = link.NavigateUri?.OriginalString;
        if (destination is null) return;
        var label = new TextRange(link.ContentStart, link.ContentEnd).Text.TrimEnd('\r', '\n');
        var dialog = new NoteLinkEditorDialog(_appearanceSettings, _noteColor, _uiStyle, label, destination)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var document = ReadEditorDocument();
            var updated = NoteLinkFormatting.UpdateLink(document, range.Value.Start, range.Value.Length,
                dialog.LinkLabel, dialog.Destination);
            NotesSnapshotValidator.ValidateDocument(updated);
            CommitLinkDocument(updated, range.Value.Start + dialog.LinkLabel.Length, 0);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            ShowLinkFeedback(exception.Message);
        }
    }

    private void RemoveLink(Hyperlink link)
    {
        var range = GetLinkRange(link);
        if (range is null) return;
        try
        {
            var document = ReadEditorDocument();
            var updated = NoteLinkFormatting.RemoveLink(document, range.Value.Start, range.Value.Length);
            CommitLinkDocument(updated, range.Value.Start, range.Value.Length);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            ShowLinkFeedback(exception.Message);
        }
    }

    private (int Start, int Length)? GetLinkRange(Hyperlink link)
    {
        var selectionStart = Editor.Selection.Start;
        var selectionEnd = Editor.Selection.End;
        _lookingUpLinkRange = true;
        try
        {
            Editor.Selection.Select(link.ContentStart, link.ContentEnd);
            return GetSelectionOffsets();
        }
        finally
        {
            Editor.Selection.Select(selectionStart, selectionEnd);
            _lookingUpLinkRange = false;
        }
    }

    private void CommitLinkDocument(NotesDocument document, int selectionStart, int selectionLength)
    {
        SetEditorDocument(document, selectionStart, selectionLength);
        SaveEditorDocument(document);
        UpdateToolbar();
        ClearLinkFeedback();
    }

    private void TrackPlainUrlInput(NotesDocument document, (int Start, int Length) input)
    {
        var previous = _pendingPlainUrl;
        var (caret, selectionLength) = GetSelectionOffsets();
        var candidate = selectionLength == 0 ? NoteLinkFormatting.FindPlainUrlEndingAt(document, caret) : null;
        if (candidate is { } current && Overlaps(input, current))
        {
            _pendingPlainUrl = current;
            _completedPlainUrl = null;
            return;
        }

        var text = document.GetPlainText();
        if (previous is { } pending && input.Length > 0 && input.Start == pending.Start + pending.Length &&
            IsBoundaryInsertion(text, input))
        {
            _completedPlainUrl = pending;
            _pendingPlainUrl = null;
            return;
        }

        _pendingPlainUrl = null;
        _completedPlainUrl = null;
    }

    private void CompletePlainUrlWhenCaretLeaves()
    {
        if (_pendingPlainUrl is not { } pending || _plainUrlCommitPending || _updatingEditor ||
            _editorRecoveryPending)
            return;
        var (start, length) = GetSelectionOffsets();
        var end = start + length;
        if (start >= pending.Start && end <= pending.Start + pending.Length) return;

        _plainUrlCommitPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            _plainUrlCommitPending = false;
            CommitPendingPlainUrl(pending);
        }));
    }

    private void OnEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_pendingPlainUrl is not { } pending) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            CommitPendingPlainUrl(pending)));
    }

    private void CommitPendingPlainUrl((int Start, int Length) pending)
    {
        if (_pendingPlainUrl != pending || _editorRecoveryPending) return;
        if (_markupFormattingPending) ApplyPendingMarkupFormatting();
        if (_pendingPlainUrl != pending) return;

        try
        {
            var document = ReadEditorDocument();
            var (selectionStart, selectionLength) = GetSelectionOffsets();
            var updated = NoteLinkFormatting.Apply(document, pending, ref selectionStart, ref selectionLength,
                forcePlainUrl: pending);
            _pendingPlainUrl = null;
            if (ReferenceEquals(updated, document)) return;
            CommitLinkDocument(updated, selectionStart, selectionLength);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            _pendingPlainUrl = null;
            ShowLinkFeedback(exception.Message);
        }
    }

    private void ShowLinkFeedback(string message)
    {
        LinkStatus.Text = message;
        LinkStatus.Visibility = Visibility.Visible;
    }

    private void ClearLinkFeedback()
    {
        if (LinkStatus is null) return;
        LinkStatus.Text = string.Empty;
        LinkStatus.Visibility = Visibility.Collapsed;
    }

    private static bool Overlaps((int Start, int Length) first, (int Start, int Length) second) =>
        first.Length > 0 && second.Length > 0 && first.Start < second.Start + second.Length &&
        second.Start < first.Start + first.Length;

    private static bool IsBoundaryInsertion(string text, (int Start, int Length) input)
    {
        if (input.Start < 0 || input.Start >= text.Length || input.Length <= 0) return false;
        var first = text[input.Start];
        return char.IsWhiteSpace(first) || first is '.' or ',' or ';' or ':' or '!' or '?';
    }

    private static void OpenLinkInDefaultBrowser(string destination) =>
        Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true, Verb = "open" });
}
