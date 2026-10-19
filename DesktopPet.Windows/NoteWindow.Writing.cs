using System.Windows.Input;

namespace DesktopPet.Windows;

public partial class NoteWindow
{
    internal event Action<object, bool>? WritingActivityChanged;
    private object? _writingField;
    private object? _writingKeyboardFocus;
    private bool _writingHidden;

    private void InitializeWritingActivity()
    {
        AddHandler(TextCompositionManager.PreviewTextInputEvent, new TextCompositionEventHandler((_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Text) && ReferenceEquals(args.OriginalSource, Keyboard.FocusedElement))
                ReportWritingInput();
        }), handledEventsToo: true);
        AddHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, args) =>
        {
            if (ReferenceEquals(_writingKeyboardFocus, args.OldFocus) &&
                !ReferenceEquals(_writingKeyboardFocus, Keyboard.FocusedElement)) StopWritingActivity();
        }), handledEventsToo: true);
        Deactivated += (_, _) => StopWritingActivity();
        IsVisibleChanged += (_, _) => { if (!IsVisible) StopWritingActivity(); };
        Editor.SelectionChanged += (_, _) =>
        {
            if (_writingField is not null && !ReferenceEquals(_writingField, GetWritingField())) StopWritingActivity();
        };
    }

    private void ReportWritingKey(Key key, ModifierKeys modifiers)
    {
        if (key is Key.Enter or Key.Tab or Key.Back or Key.Delete ||
            (key == Key.V && modifiers == ModifierKeys.Control) ||
            (key == Key.Insert && modifiers == ModifierKeys.Shift))
            ReportWritingInput();
    }

    private void ReportWritingInput()
    {
        if (GetWritingField() is not { } field) return;
        _writingField = field;
        _writingKeyboardFocus = Keyboard.FocusedElement;
        WritingActivityChanged?.Invoke(_writingField, true);
    }

    private object? GetWritingField()
    {
        if (_initializing || !IsActive || !IsVisible || _writingHidden) return null;
        if (ReferenceEquals(Keyboard.FocusedElement, NoteTitle)) return NoteTitle;
        if (!ReferenceEquals(Keyboard.FocusedElement, Editor) || _editorRecoveryPending) return null;
        if (_activeMathSource is { } source && IsInsideMathSource(Editor.Selection.Start, source) &&
            IsInsideMathSource(Editor.Selection.End, source)) return source;
        return Editor;
    }

    private void SetWritingVisibility(bool visible)
    {
        _writingHidden = !visible;
        if (!visible) StopWritingActivity();
    }

    private void StopWritingActivity(object? expectedOwner = null)
    {
        if (_writingField is not { } owner || expectedOwner is not null && !ReferenceEquals(owner, expectedOwner)) return;
        _writingField = null;
        _writingKeyboardFocus = null;
        WritingActivityChanged?.Invoke(owner, false);
    }
}
