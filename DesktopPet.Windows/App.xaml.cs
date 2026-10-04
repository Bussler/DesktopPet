using System.Configuration;
using System.IO;
using System.Windows;
using DesktopPet.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DesktopPet.Windows;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly TimeSpan NotesTransitionDuration = TimeSpan.FromMilliseconds(240);
    private HoodSession? _session;
    private NotesApplication? _notes;
    private HoodOverlayWindow? _petWindow;
    private SettingsWindow? _settingsWindow;
    private NotesSettingsWindow? _notesSettingsWindow;
    private NotesListWindow? _notesListWindow;
    private YamlConfigurationStore? _configurationStore;
    private readonly Dictionary<Guid, NoteWindow> _noteWindows = [];
    private readonly NotesVisibilityTransitionCoordinator _visibilityTransitions = new();
    private Forms.NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _trayIconImage;
    private readonly WindowsStartupRegistration _startupRegistration = new();
    private string _effectiveUiStyle = UiStyleCatalog.StickyNotes;
    private NotesConfiguration _effectiveNotesSettings = HoodConfiguration.Defaults.Notes;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var storageOverride = Environment.GetEnvironmentVariable("HOOD_DATA_DIRECTORY");
        var storageDirectory = string.IsNullOrWhiteSpace(storageOverride)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoodDesktopPet")
            : Path.GetFullPath(storageOverride);
        var configurationPath = Path.Combine(storageDirectory, "config.yaml");
        try
        {
            var configurationStore = _configurationStore = new YamlConfigurationStore(configurationPath);
            var configuration = configurationStore.LoadOrCreate();
            _effectiveUiStyle = configuration.UiStyle;
            _effectiveNotesSettings = configuration.Notes;
            try
            {
                _startupRegistration.SetEnabled(configuration.Behavior.StartWithWindows);
            }
            catch (IOException exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood's saved Windows startup preference could not be applied. The app can still run, but startup may not match config.yaml. Open Settings and try again.\n\n{exception.Message}",
                    "Windows startup setting mismatch",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            var notesStore = new JsonNotesStore(Path.Combine(storageDirectory, "notes.json"));
            var defaultNoteColor = NoteColorPalette.Parse(configuration.Notes.DefaultColor);
            try
            {
                _notes = NotesApplication.Start(notesStore, defaultNoteColor);
            }
            catch (NotesStoreException exception)
            {
                if (!TryRecoverNotes(notesStore, exception))
                {
                    Shutdown();
                    return;
                }

                _notes = NotesApplication.Start(notesStore, defaultNoteColor);
            }

            var statePath = Path.Combine(storageDirectory, "state.json");
            var sessionStore = new JsonSessionStore(statePath);
            if (!TryLoadSessionState(sessionStore, out var savedState))
            {
                Shutdown();
                return;
            }

            var assets = HoodAssets.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Hood"));
            var displays = WindowsDisplayProvider.GetDisplays();
            _notes?.RecoverNotePlacements(displays);
            try
            {
                _session = HoodSession.Start(
                    assets.Definition,
                    displays,
                    new SystemRandomSource(),
                    sessionStore,
                    configuration,
                    savedState);
            }
            catch (InvalidDataException exception) when (savedState is not null)
            {
                var invalidState = new SessionStateException(
                    statePath,
                    $"The saved animation state cannot be used with the packaged Hood assets. {exception.Message} Choose Reset saved session to archive it and start fresh.",
                    exception);
                if (!TryResetSessionState(sessionStore, invalidState))
                {
                    Shutdown();
                    return;
                }

                _session = HoodSession.Start(
                    assets.Definition,
                    displays,
                    new SystemRandomSource(),
                    sessionStore,
                    configuration);
            }

            _petWindow = new HoodOverlayWindow(
                _session,
                assets,
                ShowSettings,
                RequestQuit,
                ActivateNotes,
                CreateNote,
                ShowNotesList,
                ShowNotesSettings,
                _effectiveUiStyle);
            _petWindow.Closed += (_, _) => RequestQuit();
            MainWindow = _petWindow;
            _petWindow.Show();
            RestoreOpenNotes();
            _trayIcon = CreateTrayIcon();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
        catch (SessionStateException exception)
        {
            System.Windows.MessageBox.Show(
                $"Hood could not recover its saved session. {exception.Message}",
                "Hood session recovery failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"Hood could not start. Check the configuration at {configurationPath}, packaged assets, and display settings, then relaunch.\n\n{exception.Message}",
                "Hood could not start",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static bool TryLoadSessionState(JsonSessionStore store, out HoodSessionSnapshot? snapshot)
    {
        try
        {
            snapshot = store.Load();
            return true;
        }
        catch (SessionStateException exception)
        {
            snapshot = null;
            return TryResetSessionState(store, exception);
        }
    }

    private static bool TryResetSessionState(JsonSessionStore store, SessionStateException exception)
    {
        var recoveryWindow = new StateRecoveryWindow(exception.StatePath, exception.Message);
        if (recoveryWindow.ShowDialog() != true)
        {
            return false;
        }

        store.ResetAfterAcknowledgement();
        return true;
    }

    private static bool TryRecoverNotes(JsonNotesStore store, NotesStoreException exception)
    {
        var recoveryWindow = new NotesRecoveryWindow(exception.StorePath, exception.Message);
        if (recoveryWindow.ShowDialog() != true)
        {
            return false;
        }

        try
        {
            store.ResetAfterAcknowledgement();
            return true;
        }
        catch (NotesStoreException archiveException)
        {
            System.Windows.MessageBox.Show(
                $"Hood could not archive the damaged notes, so it did not start with empty notes. The original file was left in place.\n\n{archiveException.Message}",
                "Notes archive failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _trayIconImage?.Dispose();
        _trayIconImage = null;
        base.OnExit(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_session is null || _petWindow is null)
            {
                return;
            }

            DisplayInfo[] displays;
            try
            {
                displays = WindowsDisplayProvider.GetDisplays();
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood could not read the display layout.\n\n{exception.Message}",
                    "Hood display recovery failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            try
            {
                _session.UpdateDisplays(displays);
                _petWindow.ApplyPlacement();
            }
            catch (SessionStateException exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood moved to a usable display, but the new placement was not saved. The previous state file was retained.\n\n{exception.Message}",
                    "Hood placement save failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood could not recover its placement after the display layout changed.\n\n{exception.Message}",
                    "Hood display recovery failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            try
            {
                var placementsChanged = _notes?.RecoverNotePlacements(displays) == true;
                if (_notes is not null)
                {
                    foreach (var note in _notes.Notes)
                    {
                        if (_noteWindows.TryGetValue(note.Id, out var window))
                        {
                            window.ApplyRecoveredBounds(note.Bounds);
                            window.SetSaveStatus(_notes.LastSaveError);
                        }
                    }

                    RestoreOpenNotes();
                }

                if (placementsChanged && _notes?.LastSaveError is { } noteError)
                {
                    System.Windows.MessageBox.Show(
                        $"One or more note placements were recovered in memory, but the notes file could not be updated. The previous file was retained.\n\n{noteError}",
                        "Note placement save failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood could not recover note placements after the display layout changed.\n\n{exception.Message}",
                    "Note display recovery failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        });
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (!string.Equals(_effectiveNotesSettings.Theme, "system", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Dispatcher.BeginInvoke(ApplyNotesAppearance);
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        using var imageStream = typeof(App).Assembly.GetManifestResourceStream("DesktopPet.Hood.ico")!;
        _trayIconImage = new System.Drawing.Icon(imageStream, Forms.SystemInformation.SmallIconSize);

        var menu = new Forms.ContextMenuStrip();
        var settings = new Forms.ToolStripMenuItem("Settings");
        settings.Click += (_, _) => ShowSettings();
        var quit = new Forms.ToolStripMenuItem("Quit");
        quit.Click += (_, _) => RequestQuit();
        menu.Items.Add(settings);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(quit);
        if (_session is not null)
        {
            TrayMenuAppearance.Apply(menu, _effectiveNotesSettings.Theme, _effectiveUiStyle);
        }

        var icon = new Forms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "Hood",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => ShowSettings();
        return icon;
    }

    private void ShowSettings()
    {
        if (_session is null || _petWindow is null)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var store = _configurationStore ?? new YamlConfigurationStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HoodDesktopPet",
            "config.yaml"));
        _settingsWindow = new SettingsWindow(
            new HoodConfigurationController(_session, store, _startupRegistration), _petWindow, _effectiveUiStyle, ApplySavedAppearance);
        _settingsWindow.ApplyTheme(_effectiveNotesSettings.Theme, _effectiveUiStyle);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowNotesSettings()
    {
        if (_session is null)
        {
            return;
        }

        if (_notesSettingsWindow is { IsVisible: true })
        {
            _notesSettingsWindow.Activate();
            return;
        }

        var store = _configurationStore ?? new YamlConfigurationStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HoodDesktopPet",
            "config.yaml"));
        _notesSettingsWindow = new NotesSettingsWindow(
            new HoodConfigurationController(_session, store, _startupRegistration),
            ApplySavedNotesAppearance,
            _effectiveUiStyle,
            _effectiveNotesSettings);
        _notesSettingsWindow.Closed += (_, _) => _notesSettingsWindow = null;
        _notesSettingsWindow.Show();
        _notesSettingsWindow.Activate();
    }

    private void ApplyNotesAppearance()
    {
        if (_session is null || _notes is null)
        {
            return;
        }

        var settings = _effectiveNotesSettings;
        _notesListWindow?.ApplyTheme(settings.Theme, _effectiveUiStyle);
        _notesSettingsWindow?.ApplyEffectiveSettings(settings, _effectiveUiStyle);
        _settingsWindow?.ApplyTheme(settings.Theme, _effectiveUiStyle);
        if (_trayIcon?.ContextMenuStrip is { } trayMenu)
        {
            TrayMenuAppearance.Apply(trayMenu, settings.Theme, _effectiveUiStyle);
        }
        _petWindow?.ApplyAppearance(settings.Theme, _effectiveUiStyle);
        _notes.SetDefaultColor(NoteColorPalette.Parse(settings.DefaultColor));
        foreach (var (noteId, window) in _noteWindows)
        {
            window.ApplyAppearance(settings, _notes.GetNote(noteId).Color, _effectiveUiStyle);
        }
    }

    private void ApplySavedNotesAppearance()
    {
        if (_session is null || _effectiveNotesSettings == _session.Configuration.Notes)
        {
            return;
        }

        var previousSettings = _effectiveNotesSettings;
        _effectiveNotesSettings = _session.Configuration.Notes;
        try
        {
            ApplyNotesAppearance();
        }
        catch (Exception applyError) when (applyError is not OutOfMemoryException)
        {
            _effectiveNotesSettings = previousSettings;
            try
            {
                ApplyNotesAppearance();
            }
            catch (Exception restoreError) when (restoreError is not OutOfMemoryException)
            {
                throw new AggregateException(
                    "The Notes settings were saved, but Hood could not refresh the appearance or restore the previous one. Restart Hood to apply the saved settings.",
                    applyError,
                    restoreError);
            }

            throw new InvalidOperationException(
                "The Notes settings were saved, but Hood could not apply them. The previous appearance was restored; restart Hood to apply the saved settings.",
                applyError);
        }
    }

    private void ApplySavedAppearance()
    {
        if (_session is null || string.Equals(_session.Configuration.UiStyle, _effectiveUiStyle, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousStyle = _effectiveUiStyle;
        _effectiveUiStyle = _session.Configuration.UiStyle;
        try
        {
            ApplyNotesAppearance();
        }
        catch (Exception applyError) when (applyError is not OutOfMemoryException)
        {
            _effectiveUiStyle = previousStyle;
            try
            {
                ApplyNotesAppearance();
            }
            catch (Exception restoreError) when (restoreError is not OutOfMemoryException)
            {
                throw new AggregateException(
                    "The style was saved, but Hood could not refresh the appearance or restore the previous one. Restart Hood to apply the saved style.",
                    applyError,
                    restoreError);
            }

            throw new InvalidOperationException(
                $"The style was saved, but Hood could not apply it. The previous {previousStyle} appearance was restored; restart Hood to apply the saved style.",
                applyError);
        }
    }

    private void RequestQuit()
    {
        _visibilityTransitions.Request(visible: false);
        if (_notes is not null && !_notes.Flush())
        {
            System.Windows.MessageBox.Show(
                $"Hood could not save the latest notes. The app will still exit, so unsaved edits may be lost. The previous notes file was retained.\n\n{_notes.LastSaveError}",
                "Notes save failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }

        if (_session is { QuitRequested: false })
        {
            try
            {
                _session.Quit();
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Hood could not save the session. The app will still exit.\n\n{exception.Message}",
                    "Hood session save failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        foreach (var window in _noteWindows.Values.ToArray())
        {
            window.CloseWithoutMembershipChange();
        }

        _noteWindows.Clear();
        _settingsWindow?.Close();
        _settingsWindow = null;
        _notesSettingsWindow?.Close();
        _notesSettingsWindow = null;
        _notesListWindow?.Close();
        _notesListWindow = null;

        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    private void ActivateNotes()
    {
        if (_notes is null)
        {
            return;
        }

        var result = _notes.Activate();
        RestoreOpenNotes(animateGroupTransition: result.Kind != NotesActivationKind.ShowNotesList);
        if (result.Kind == NotesActivationKind.ShowNotesList)
        {
            ShowNotesList();
        }

        if (_notes.LastSaveError is { } error)
        {
            System.Windows.MessageBox.Show(
                $"The notes action remains available in memory, but the latest notes could not be saved. Reopen the notes group to retry, or use Retry save in a note or the Notes list.\n\n{error}",
                "Notes save failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void CreateNote()
    {
        var wasVisible = _notes?.GroupVisible == true;
        try
        {
            _notes?.CreateNote();
        }
        catch (InvalidDataException exception)
        {
            System.Windows.MessageBox.Show(exception.Message, "Cannot create note", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RestoreOpenNotes(animateGroupTransition: !wasVisible);
        _notesListWindow?.Refresh();
    }

    private void ShowNotesList()
    {
        if (_notes is null)
        {
            return;
        }

        if (_notesListWindow is { IsVisible: true })
        {
            _notesListWindow.Refresh();
            _notesListWindow.SelectNotes();

            _notesListWindow.Activate();
            return;
        }

        _notesListWindow = new NotesListWindow(
            _notes.Search,
            _notes.SearchTrash,
            OpenNoteFromList,
            CreateNote,
            RestoreTrashedNote,
            PermanentlyDeleteTrashedNote,
            RetryNotesSave,
            _effectiveNotesSettings.Theme,
            moveNoteToTrash: MoveNoteToTrash,
            uiStyle: _effectiveUiStyle);
        _notesListWindow.Closed += (_, _) => _notesListWindow = null;
        _notesListWindow.Show();
        _notesListWindow.Activate();
    }

    private void OpenNoteFromList(Guid noteId)
    {
        var wasVisible = _notes?.GroupVisible == true;
        _notes?.OpenNote(noteId);
        RestoreOpenNotes(animateGroupTransition: !wasVisible);
        _notesListWindow?.Refresh();
    }

    private void RestoreOpenNotes(bool animateGroupTransition = false)
    {
        if (_notes is null)
        {
            return;
        }

        var openIds = _notes.OpenNoteIds.ToHashSet();
        foreach (var noteId in _noteWindows.Keys.Where(noteId => !openIds.Contains(noteId)).ToArray())
        {
            _noteWindows[noteId].SetGroupVisibilityImmediately(false);
        }

        foreach (var note in _notes.Notes.Where(note => openIds.Contains(note.Id)))
        {
            if (!_noteWindows.TryGetValue(note.Id, out var window))
            {
                window = new NoteWindow(
                    note,
                    UpdateNoteDocument,
                    UpdateNoteName,
                    UpdateNoteBounds,
                    EditDocumentHistory,
                    TryCloseNote,
                    CreateNote,
                    ShowNotesList,
                    ShowNotesSettings,
                    UpdateNoteColor,
                    MoveNoteToTrash,
                    RetryNotesSave,
                    _effectiveNotesSettings,
                    _effectiveUiStyle);
                _noteWindows.Add(note.Id, window);
            }

            window.SetSaveStatus(_notes.LastSaveError);
        }

        var animate = animateGroupTransition && _effectiveNotesSettings.AnimateShowHide;
        var transition = _visibilityTransitions.Request(_notes.GroupVisible);
        if (!animate)
        {
            foreach (var noteId in openIds)
            {
                _noteWindows[noteId].SetGroupVisibilityImmediately(_notes.GroupVisible);
            }

            return;
        }

        var hoodCenter = _petWindow?.PresentationCenter ?? new System.Windows.Point(96, 96);
        foreach (var noteId in openIds)
        {
            var window = _noteWindows[noteId];
            window.BeginGroupVisibilityTransition(
                _notes.GroupVisible,
                hoodCenter,
                NotesTransitionDuration,
                () => CompleteVisibilityTransition(noteId, transition));
        }
    }

    private void CompleteVisibilityTransition(Guid noteId, NotesVisibilityTransition transition)
    {
        if (_notes is null || !_visibilityTransitions.IsCurrent(transition) || !_notes.OpenNoteIds.Contains(noteId))
        {
            return;
        }

        if (!transition.Visible && _noteWindows.TryGetValue(noteId, out var window))
        {
            window.Hide();
        }
    }

    private string? UpdateNoteDocument(Guid noteId, NotesDocument document)
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        try
        {
            _notes.UpdateDocument(noteId, document);
            _notesListWindow?.Refresh();
            return _notes.LastSaveError;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return exception.Message;
        }
    }

    private string? UpdateNoteName(Guid noteId, string name)
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        try
        {
            _notes.UpdateName(noteId, name);
            _notesListWindow?.Refresh();
            return _notes.LastSaveError;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return exception.Message;
        }
    }

    private string? UpdateNoteColor(Guid noteId, NoteColor color)
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        try
        {
            return _notes.UpdateColor(noteId, color);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return exception.Message;
        }
    }

    private string? MoveNoteToTrash(Guid noteId)
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        if (!_notes.TryMoveToTrash(noteId, out var error))
        {
            return error;
        }

        if (_noteWindows.Remove(noteId, out var window))
        {
            window.CloseWithoutMembershipChange();
        }

        _notesListWindow?.Refresh();
        if (_notes.OpenNoteIds.Count == 0)
        {
            ShowNotesList();
        }

        return null;
    }

    private string? RestoreTrashedNote(Guid noteId)
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        if (!_notes.TryRestoreFromTrash(noteId, out var error))
        {
            return error;
        }

        _notesListWindow?.Refresh();
        return null;
    }

    private string? PermanentlyDeleteTrashedNote(Guid noteId)
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        if (!_notes.TryPermanentlyDeleteFromTrash(noteId, confirmed: true, out var error))
        {
            return error;
        }

        _notesListWindow?.Refresh();
        return null;
    }

    private (NotesDocument? Document, string? Error) EditDocumentHistory(Guid noteId, bool undo)
    {
        if (_notes is null)
        {
            return (null, "Notes are not available.");
        }

        var changed = undo ? _notes.Undo(noteId) : _notes.Redo(noteId);
        _notesListWindow?.Refresh();
        return changed ? (_notes.GetNote(noteId).Document, _notes.LastSaveError) : (null, _notes.LastSaveError);
    }

    private string? UpdateNoteBounds(Guid noteId, NoteBounds bounds)
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        _notes.UpdateBounds(noteId, bounds);
        return _notes.LastSaveError;
    }

    private string? RetryNotesSave()
    {
        if (_notes is null)
        {
            return "Notes are not available.";
        }

        _notes.Flush();
        foreach (var window in _noteWindows.Values)
        {
            window.SetSaveStatus(_notes.LastSaveError);
        }

        RestoreOpenNotes();
        _notesListWindow?.SetSaveStatus(_notes.LastSaveError);
        return _notes.LastSaveError;
    }

    private (bool Allowed, string? Error) TryCloseNote(Guid noteId)
    {
        if (_notes is null)
        {
            return (true, null);
        }

        if (!_notes.TryCloseNote(noteId, out var error))
        {
            return (false, error);
        }

        _noteWindows.Remove(noteId);
        _notesListWindow?.Refresh();
        return (true, null);
    }
}

