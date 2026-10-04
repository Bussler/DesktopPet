using System.Windows;
using System.Windows.Input;
using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class NotesListWindow : Window
{
    private readonly Func<string?, IReadOnlyList<NoteListItem>> _search;
    private readonly Func<string?, IReadOnlyList<TrashedNoteListItem>> _searchTrash;
    private readonly Action<Guid> _openNote;
    private readonly Action _createNote;
    private readonly Func<Guid, string?> _restoreNote;
    private readonly Func<Guid, string?> _permanentlyDeleteNote;
    private readonly Func<string?> _retrySave;
    private readonly Func<Guid, string?>? _moveNoteToTrash;
    private readonly Func<int, bool> _confirmPermanentDelete;
    private string _uiStyle;

    public NotesListWindow(
        Func<string?, IReadOnlyList<NoteListItem>> search,
        Func<string?, IReadOnlyList<TrashedNoteListItem>> searchTrash,
        Action<Guid> openNote,
        Action createNote,
        Func<Guid, string?> restoreNote,
        Func<Guid, string?> permanentlyDeleteNote,
        Func<string?> retrySave,
        string theme = "system",
        Func<Guid, string?>? moveNoteToTrash = null,
        Func<int, bool>? confirmPermanentDelete = null,
        string uiStyle = UiStyleCatalog.StickyNotes)
    {
        _search = search;
        _searchTrash = searchTrash;
        _openNote = openNote;
        _createNote = createNote;
        _restoreNote = restoreNote;
        _permanentlyDeleteNote = permanentlyDeleteNote;
        _retrySave = retrySave;
        _moveNoteToTrash = moveNoteToTrash;
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        _confirmPermanentDelete = confirmPermanentDelete ?? (count => System.Windows.MessageBox.Show(
            this,
            count == 1 ? "Permanently delete this note? This cannot be undone."
                : $"Permanently delete these {count} selected notes? This cannot be undone.",
            "Delete notes permanently",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes);
        InitializeComponent();
        ApplyTheme(theme, _uiStyle);
        Refresh();
        Loaded += (_, _) => SearchBox.Focus();
    }

    public void ApplyTheme(string theme, string? style = null)
    {
        if (style is not null)
        {
            _uiStyle = UiStyleCatalog.Canonicalize(style);
        }

        NotesAppearance.Apply(this, theme, _uiStyle);
    }

    public void SelectNotes()
    {
        Views.SelectedIndex = 0;
        UpdateSearchLabel();
        Refresh();
    }

    public void Refresh()
    {
        if (SearchBox is null || ResultsList is null || TrashResultsList is null)
        {
            return;
        }

        var query = SearchBox?.Text;
        var notes = _search(query);
        var trash = _searchTrash(query);
        var selectedNotes = ResultsList.SelectedItems.OfType<NoteListItem>().Select(item => item.Id).ToHashSet();
        var selectedTrash = TrashResultsList.SelectedItems.OfType<TrashedNoteListItem>().Select(item => item.Id).ToHashSet();
        ResultsList.ItemsSource = notes;
        TrashResultsList.ItemsSource = trash;
        foreach (var note in notes.Where(note => selectedNotes.Contains(note.Id)))
            ResultsList.SelectedItems.Add(note);
        foreach (var note in trash.Where(note => selectedTrash.Contains(note.Id)))
            TrashResultsList.SelectedItems.Add(note);

        EmptyState.Text = string.IsNullOrWhiteSpace(query)
            ? "No notes yet. Create a note to get started."
            : "No notes match your search.";
        EmptyState.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TrashEmptyState.Text = string.IsNullOrWhiteSpace(query)
            ? "Trash is empty. Deleted notes will appear here."
            : "No trashed notes match your search.";
        TrashEmptyState.Visibility = trash.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateActions();
    }

    private void UpdateActions()
    {
        if (Views is null)
        {
            return;
        }

        var showingNotes = Views.SelectedIndex == 0;
        ActiveActions.Visibility = showingNotes ? Visibility.Visible : Visibility.Collapsed;
        TrashActions.Visibility = showingNotes ? Visibility.Collapsed : Visibility.Visible;
        OpenButton.IsEnabled = showingNotes && ResultsList.SelectedItems.Count == 1;
        TrashButton.IsEnabled = showingNotes && ResultsList.SelectedItems.Count > 0 && _moveNoteToTrash is not null;
        RestoreButton.IsEnabled = !showingNotes && TrashResultsList.SelectedItems.Count == 1;
        PermanentlyDeleteButton.IsEnabled = !showingNotes && TrashResultsList.SelectedItems.Count > 0;
    }

    private void ShowStatus(string? error)
    {
        ManagerStatus.Text = error ?? string.Empty;
        ManagerStatus.Visibility = string.IsNullOrWhiteSpace(error) ? Visibility.Collapsed : Visibility.Visible;
        RetrySaveButton.Visibility = string.IsNullOrWhiteSpace(error) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void SetSaveStatus(string? error) => ShowStatus(error);

    private void OnRetrySaveClick(object sender, RoutedEventArgs e)
    {
        ShowStatus(_retrySave());
        Refresh();
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Refresh();

    private void OnViewsSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Views))
        {
            UpdateSearchLabel();
            Refresh();
        }
    }

    private void UpdateSearchLabel() => SearchLabel.Content = Views.SelectedIndex == 1 ? "_Search Trash" : "_Search notes";

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateActions();

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItems.Count == 1 && ResultsList.SelectedItem is NoteListItem item)
        {
            _openNote(item.Id);
            ShowStatus(null);
            Refresh();
        }
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e) => _createNote();

    private void OnTrashClick(object sender, RoutedEventArgs e)
    {
        if (_moveNoteToTrash is not null)
        {
            ApplyToSelection(ResultsList.SelectedItems.OfType<NoteListItem>().Select(item => item.Id).ToArray(), _moveNoteToTrash);
        }
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (TrashResultsList.SelectedItems.Count != 1 || TrashResultsList.SelectedItem is not TrashedNoteListItem item)
        {
            return;
        }

        var error = _restoreNote(item.Id);
        ShowStatus(error);
        if (error is null)
        {
            Views.SelectedIndex = 0;
            Refresh();
        }
    }

    private void OnPermanentlyDeleteClick(object sender, RoutedEventArgs e)
    {
        var ids = TrashResultsList.SelectedItems.OfType<TrashedNoteListItem>().Select(item => item.Id).ToArray();
        if (ids.Length == 0 || !_confirmPermanentDelete(ids.Length))
        {
            return;
        }

        ApplyToSelection(ids, _permanentlyDeleteNote);
    }

    private void ApplyToSelection(Guid[] ids, Func<Guid, string?> action)
    {
        if (ids.Length == 0) return;
        string? error = null;
        var completed = 0;
        foreach (var id in ids)
        {
            error = action(id);
            if (error is not null) break;
            completed++;
        }
        ShowStatus(error is null || ids.Length == 1 ? error : $"{completed} of {ids.Length} notes processed. {error}");
        Refresh();
    }

    private void OnResultsDoubleClick(object sender, MouseButtonEventArgs e) => OnOpenClick(sender, e);

    private void OnResultsKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ResultsList.SelectedItem is NoteListItem)
        {
            OnOpenClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnTrashResultsKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && TrashResultsList.SelectedItem is TrashedNoteListItem)
        {
            OnRestoreClick(sender, e);
            e.Handled = true;
        }
    }
}
