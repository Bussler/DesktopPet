using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;
using Microsoft.Win32;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;

namespace DesktopPet.Windows;

public partial class HoodOverlayWindow : Window
{
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private readonly HoodSession _session;
    private readonly LoadedHoodAssets _assets;
    private readonly Action _showSettings;
    private readonly Action _quitApplication;
    private readonly Action _activateNotes;
    private readonly Action _createNote;
    private readonly Action _showNotesList;
    private readonly Action _showNotesSettings;
    private string _uiStyle;
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private long _lastTick;
    private int _suspended;
    private PetAction? _renderedAction;
    private int _renderedFrame = -1;
    private readonly WpfMenuItem _hungerMenuItem = new() { IsEnabled = false };
    private readonly WpfMenuItem _boredomMenuItem = new() { IsEnabled = false };
    private readonly WpfMenuItem _sleepMenuItem = new() { IsEnabled = false };
    private readonly WpfMenuItem _sleepActionMenuItem = new();
    private readonly WpfMenuItem _editAreaMenuItem = new();
    private RoamingAreaEditorWindow? _areaEditor;
    private System.Windows.Point _mouseDownScreen;
    private System.Windows.Point _lastPointerScreen;
    private PetPosition _dragStartPosition;
    private bool _trackingMouse;
    private bool _dragged;

    internal HoodOverlayWindow(
        HoodSession session,
        LoadedHoodAssets assets,
        Action showSettings,
        Action quitApplication,
        Action activateNotes,
        Action createNote,
        Action showNotesList,
        Action showNotesSettings)
        : this(session, assets, showSettings, quitApplication, activateNotes, createNote, showNotesList, showNotesSettings, UiStyleCatalog.StickyNotes)
    {
    }

    internal HoodOverlayWindow(
        HoodSession session,
        LoadedHoodAssets assets,
        Action showSettings,
        Action quitApplication,
        Action activateNotes,
        Action createNote,
        Action showNotesList,
        Action showNotesSettings,
        string uiStyle)
    {
        _session = session;
        _assets = assets;
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        _showSettings = showSettings;
        _quitApplication = quitApplication;
        _activateNotes = activateNotes;
        _createNote = createNote;
        _showNotesList = showNotesList;
        _showNotesSettings = showNotesSettings;
        InitializeComponent();
        Width = PetImage.Width = assets.Definition.DisplayWidth;
        Height = PetImage.Height = assets.Definition.DisplayHeight;
        Topmost = session.Configuration.Behavior.AlwaysOnTop;
        Left = session.Position.X;
        Top = session.Position.Y;
        ContextMenu = CreatePetMenu();
        ContextMenu.Opened += (_, _) => UpdateNeedLabels();
        Closed += OnOverlayClosed;
        _frameTimer.Tick += OnFrameTimerTick;
        RenderPresentation();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowMessageHook);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lastTick = Stopwatch.GetTimestamp();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        UpdateTimerInterval();
        _frameTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _frameTimer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        _session.EndDragging();
        _session.EndAreaEditing();
        _areaEditor?.Close();
    }

    private void OnFrameTimerTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var previous = Interlocked.Exchange(ref _lastTick, now);
        if (Volatile.Read(ref _suspended) != 0)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(previous, now);
        var oldPosition = _session.Position;
        try
        {
            _session.Advance(elapsed);
        }
        catch (SessionStateException exception)
        {
            ShowSaveFailure(exception);
        }

        if (_session.Position != oldPosition)
        {
            ApplyPlacement();
        }

        RenderPresentation();
        UpdateTimerInterval();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            Interlocked.Exchange(ref _suspended, 1);
            Interlocked.Exchange(ref _lastTick, Stopwatch.GetTimestamp());
        }
        else if (e.Mode == PowerModes.Resume)
        {
            Interlocked.Exchange(ref _lastTick, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref _suspended, 0);
        }
    }

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _trackingMouse = true;
        _dragged = false;
        _mouseDownScreen = PointToScreen(e.GetPosition(this));
        _lastPointerScreen = _mouseDownScreen;
        _dragStartPosition = _session.Position;
        _session.BeginDragging();
        CaptureMouse();
        UpdateTimerInterval();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_trackingMouse || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        var pointer = PointToScreen(e.GetPosition(this));
        var scale = VisualTreeHelper.GetDpi(this);
        var total = pointer - _mouseDownScreen;
        if (!_dragged)
        {
            if (Math.Abs(total.X) < SystemParameters.MinimumHorizontalDragDistance * scale.DpiScaleX &&
                Math.Abs(total.Y) < SystemParameters.MinimumVerticalDragDistance * scale.DpiScaleY)
            {
                _lastPointerScreen = pointer;
                return;
            }

            _dragged = true;
            _session.MoveTo(new PetPosition(
                _dragStartPosition.X + total.X / scale.DpiScaleX,
                _dragStartPosition.Y + total.Y / scale.DpiScaleY),
                persist: false);
        }
        else
        {
            var delta = pointer - _lastPointerScreen;
            _session.MoveTo(new PetPosition(
                _session.Position.X + delta.X / scale.DpiScaleX,
                _session.Position.Y + delta.Y / scale.DpiScaleY),
                persist: false);
        }

        _lastPointerScreen = pointer;
        ApplyPlacement();
        UpdateTimerInterval();
    }

    private void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_trackingMouse)
        {
            return;
        }

        _trackingMouse = false;
        ReleaseMouseCapture();
        _session.EndDragging();
        if (_dragged)
        {
            RunSessionCommand(_session.SaveCurrentState);
        }
        else
        {
            RunSessionCommand(_session.React);
            _activateNotes();
        }

        _dragged = false;
        UpdateTimerInterval();
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_trackingMouse)
        {
            return;
        }

        _trackingMouse = false;
        _session.EndDragging();
        if (_dragged)
        {
            RunSessionCommand(_session.SaveCurrentState);
        }

        _dragged = false;
        UpdateTimerInterval();
    }

    private WpfContextMenu CreatePetMenu()
    {
        var menu = new WpfContextMenu();
        NotesAppearance.Apply(menu, _session.Configuration.Notes.Theme, _uiStyle);
        menu.Items.Add(new WpfMenuItem { Header = "Hood's needs", IsEnabled = false });
        menu.Items.Add(_hungerMenuItem);
        menu.Items.Add(_boredomMenuItem);
        menu.Items.Add(_sleepMenuItem);
        foreach (var item in menu.Items.OfType<WpfMenuItem>())
        {
            item.SetResourceReference(WpfMenuItem.ForegroundProperty, "NotesSecondaryTextBrush");
        }
        menu.Items.Add(new WpfSeparator());
        var feed = new WpfMenuItem { Header = "Feed" };
        feed.Click += (_, _) => RunSessionCommand(_session.Feed);
        menu.Items.Add(feed);
        _sleepActionMenuItem.Click += (_, _) => RunSessionCommand(_session.Sleep);
        menu.Items.Add(_sleepActionMenuItem);
        _editAreaMenuItem.Header = "Edit roaming area";
        _editAreaMenuItem.Click += (_, _) => OpenAreaEditor();
        menu.Items.Add(_editAreaMenuItem);
        menu.Items.Add(new WpfSeparator());
        var notes = new WpfMenuItem { Header = "_Notes" };
        var newNote = new WpfMenuItem { Header = "_New note" };
        newNote.Click += (_, _) => _createNote();
        var notesList = new WpfMenuItem { Header = "_Notes list" };
        notesList.Click += (_, _) => _showNotesList();
        var notesSettings = new WpfMenuItem { Header = "Notes _settings" };
        notesSettings.Click += (_, _) => _showNotesSettings();
        notes.Items.Add(newNote);
        notes.Items.Add(notesList);
        notes.Items.Add(notesSettings);
        menu.Items.Add(notes);
        menu.Items.Add(new WpfSeparator());
        var settings = new WpfMenuItem { Header = "Settings" };
        settings.Click += (_, _) => _showSettings();
        menu.Items.Add(settings);
        var quit = new WpfMenuItem { Header = "Quit" };
        quit.Click += (_, _) => _quitApplication();
        menu.Items.Add(quit);
        return menu;
    }

    public void ApplyAppearance(string theme, string uiStyle)
    {
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        if (ContextMenu is { } menu)
        {
            NotesAppearance.Apply(menu, theme, _uiStyle);
        }
    }

    private void UpdateNeedLabels()
    {
        _hungerMenuItem.Header = FormatNeed("Hunger", _session.Needs.Hunger, "content", "getting hungry", "hungry");
        _boredomMenuItem.Header = FormatNeed("Boredom", _session.Needs.Boredom, "content", "getting restless", "could use something to do");
        _sleepMenuItem.Header = FormatNeed("Sleep", _session.Needs.Sleep, "rested", "winding down", "sleepy");
        _sleepActionMenuItem.Header = _session.Mode == HoodSessionMode.Sleeping ? "Already sleeping" : "Sleep";
        _sleepActionMenuItem.IsEnabled = _session.Mode == HoodSessionMode.Awake;
    }

    private static string FormatNeed(string name, double value, string low, string medium, string high)
    {
        var status = value < 25 ? low : value < 60 ? medium : high;
        return $"{name}: {value:0.#}/100 — {status}";
    }

    private void RunSessionCommand(Action command)
    {
        try
        {
            command();
            RenderPresentation();
        }
        catch (SessionStateException exception)
        {
            ShowSaveFailure(exception);
        }

        UpdateTimerInterval();
    }

    private void OpenAreaEditor()
    {
        if (_areaEditor is { IsVisible: true })
        {
            _areaEditor.Activate();
            return;
        }

        _session.BeginAreaEditing();
        _areaEditor = new RoamingAreaEditorWindow(_session, this);
        _areaEditor.Closed += (_, _) =>
        {
            _session.EndAreaEditing();
            _areaEditor = null;
            UpdateTimerInterval();
        };
        _areaEditor.Show();
        UpdateTimerInterval();
    }

    internal void OpenRoamingAreaEditor() => OpenAreaEditor();

    private static void ShowSaveFailure(SessionStateException exception) => System.Windows.MessageBox.Show(
        $"Hood's current behavior continues, but the updated session was not saved. The previous saved state was retained.\n\n{exception.Message}",
        "Hood session save failed",
        MessageBoxButton.OK,
        MessageBoxImage.Error);

    private void RenderPresentation()
    {
        var presentation = _session.Presentation;
        Left = presentation.Position.X;
        Top = presentation.Position.Y;
        if (_renderedAction == presentation.Action && _renderedFrame == presentation.FrameIndex)
        {
            return;
        }

        var animation = _assets.Definition.GetAnimation(presentation.Action);
        var frame = animation.Frames[presentation.FrameIndex];
        var rectangle = new Int32Rect(
            frame.Column * _assets.Definition.FrameWidth,
            frame.Row * _assets.Definition.FrameHeight,
            _assets.Definition.FrameWidth,
            _assets.Definition.FrameHeight);
        var cropped = new CroppedBitmap(_assets.SpriteSheet, rectangle);
        cropped.Freeze();
        PetImage.Source = cropped;
        _renderedAction = presentation.Action;
        _renderedFrame = presentation.FrameIndex;
    }

    internal void ApplyAlwaysOnTop(bool enabled) => Topmost = enabled;

    internal System.Windows.Point PresentationCenter => new(Left + ActualWidth / 2, Top + ActualHeight / 2);

    internal void RefreshBehaviorSettings()
    {
        UpdateTimerInterval();
        RenderPresentation();
    }

    internal void ApplyPlacement()
    {
        Left = _session.Presentation.Position.X;
        Top = _session.Presentation.Position.Y;
        _areaEditor?.ApplyPlacement();
    }

    private void UpdateTimerInterval() => _frameTimer.Interval = TimeSpan.FromMilliseconds(
        _session.RequiresAnimationFrames ? 33 : 1000);

    private static IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmMouseActivate)
        {
            handled = true;
            return new IntPtr(MaNoActivate);
        }

        return IntPtr.Zero;
    }
}
