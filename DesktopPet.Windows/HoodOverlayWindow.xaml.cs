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
    private readonly IMediaAudioProbe _mediaAudioProbe;
    private readonly MediaHeadphonePolicy _mediaHeadphonePolicy = new();
    private HoodAssetSource _assetSource;
    private LoadedHoodAssets? _assets;
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
    private int _mediaObserverGeneration;
    private bool _mediaHeadphonesVisible;
    private bool _mediaHeadphonesEnabled;
    private bool _mediaObservationStopped;
    private MediaHeadphoneObserver? _mediaHeadphoneObserver;
    private BitmapSource? _renderedSpriteSheet;
    private Int32Rect? _renderedRectangle;
    private readonly WpfMenuItem _hungerMenuItem = new() { IsEnabled = false };
    private readonly WpfMenuItem _boredomMenuItem = new() { IsEnabled = false };
    private readonly WpfMenuItem _sleepMenuItem = new() { IsEnabled = false };
    private readonly WpfMenuItem _sleepActionMenuItem = new();
    private readonly WpfMenuItem _editAreaMenuItem = new();
    private readonly WpfMenuItem _showSpritesMenuItem = new() { Header = "Show Hood sprites", IsCheckable = true };
    internal event Action? ToggleSpritesRequested;
    internal event Action? SpritesVisibilityChanged;
    internal event Action? FocusRequested;
    private FocusSession? _focus;
    private PetAction? _focusAction;
    private long _focusAnimationTicks;
    private long? _checklistAnimationTicks;
    private long _checklistAnimationStartedAt;
    private RoamingAreaEditorWindow? _areaEditor;
    private System.Windows.Point _mouseDownScreen;
    private System.Windows.Point _lastPointerScreen;
    private PetPosition _dragStartPosition;
    private PetPosition _dragRequestedPosition;
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
        : this(session, assets.Source, assets,
            showSettings, quitApplication, activateNotes, createNote, showNotesList, showNotesSettings, uiStyle)
    {
    }

    internal HoodOverlayWindow(
        HoodSession session,
        HoodAssetSource assetSource,
        LoadedHoodAssets? assets,
        Action showSettings,
        Action quitApplication,
        Action activateNotes,
        Action createNote,
        Action showNotesList,
        Action showNotesSettings,
        string uiStyle)
        : this(session, assetSource, assets, showSettings, quitApplication, activateNotes, createNote,
            showNotesList, showNotesSettings, uiStyle, new WindowsMediaAudioProbe())
    {
    }

    internal HoodOverlayWindow(
        HoodSession session,
        HoodAssetSource assetSource,
        LoadedHoodAssets? assets,
        Action showSettings,
        Action quitApplication,
        Action activateNotes,
        Action createNote,
        Action showNotesList,
        Action showNotesSettings,
        string uiStyle,
        IMediaAudioProbe mediaAudioProbe,
        TimeProvider? writingClock = null)
    {
        _session = session;
        _writingClock = writingClock ?? TimeProvider.System;
        _mediaAudioProbe = mediaAudioProbe;
        _mediaHeadphonesEnabled = session.Configuration.Behavior.MediaHeadphones;
        _assetSource = assetSource;
        _assets = assets;
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        _showSettings = showSettings;
        _quitApplication = quitApplication;
        _activateNotes = activateNotes;
        _createNote = createNote;
        _showNotesList = showNotesList;
        _showNotesSettings = showNotesSettings;
        InitializeComponent();
        Width = PetImage.Width = session.PetWidth;
        Height = PetImage.Height = session.PetHeight;
        Topmost = session.Configuration.Behavior.AlwaysOnTop;
        Left = session.Position.X;
        Top = session.Position.Y;
        ContextMenu = CreatePetMenu();
        UpdateNeedLabels();
        ContextMenu.Opened += (_, _) => UpdateNeedLabels();
        Closed += OnOverlayClosed;
        _frameTimer.Tick += OnFrameTimerTick;
        if (session.Configuration.Behavior.ShowSprites) RenderPresentation();
        else ReleaseSpriteAssets();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowMessageHook);
            ApplyPlacement();
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        try
        {
            ReloadAssetsForDpi(newDpi.DpiScaleX, newDpi.DpiScaleY);
        }
        catch (PetAssetException exception)
        {
            System.Windows.MessageBox.Show(
                $"Hood could not reload its artwork for this display. The last valid appearance remains active.\n\n{exception.Message}",
                "Pet artwork could not be reloaded",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    internal void ReloadAssetsForDpi(double dpiScaleX, double dpiScaleY)
    {
        if (_assets is null || (_assets.DpiScaleX == dpiScaleX && _assets.DpiScaleY == dpiScaleY))
        {
            return;
        }

        var source = _assetSource.CachedSpriteSheet is null
            ? HoodAssets.ReadSourceForPet(_assetSource.Directory, _assetSource.Definition)
            : _assetSource;
        ApplyPetAssets(source, HoodAssets.LoadForDisplay(source, dpiScaleX, dpiScaleY, _session.PetScale));
    }

    internal void EnsureSpriteAssetsLoaded()
    {
        var dpiScale = WindowsDisplayProvider.GetScaleForSize(
            _session.Position, _session.PetWidth, _session.PetHeight, WindowsDisplayProvider.GetDisplays());
        if (_assets is not null && _assets.DpiScaleX == dpiScale && _assets.DpiScaleY == dpiScale &&
            _assets.DecodeScale == _session.PetScale)
            return;

        var source = _assetSource.CachedSpriteSheet is null
            ? HoodAssets.ReadSourceForPet(_assetSource.Directory, _assetSource.Definition)
            : _assetSource;
        ApplyPetAssets(source, HoodAssets.LoadForDisplay(source, dpiScale, dpiScale, _session.PetScale));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lastTick = Stopwatch.GetTimestamp();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        ApplyMediaHeadphones(_session.Configuration.Behavior.MediaHeadphones);
        UpdateTimerInterval();
        if (_session.Configuration.Behavior.ShowSprites)
        {
            _frameTimer.Start();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _frameTimer.Stop();
        _mediaObservationStopped = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        StopMediaHeadphoneObservation();
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        ClearWritingActivity();
        if (_focus is not null) _focus.Changed -= SyncFocusPresentation;
        _session.SetFocusPresentation(false);
        _session.EndDragging();
        _session.EndAreaEditing();
        _areaEditor?.Close();
    }

    private void OnFrameTimerTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var previous = Interlocked.Exchange(ref _lastTick, now);
        if (Volatile.Read(ref _suspended) != 0 || !_session.Configuration.Behavior.ShowSprites)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(previous, now);
        AdvanceFocusAnimation(elapsed);
        AdvanceChecklistAnimation(Stopwatch.GetElapsedTime(Math.Max(previous, _checklistAnimationStartedAt), now));
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

        UpdateWritingPresentation();
        RenderPresentation();
        UpdateTimerInterval();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        var mode = e.Mode;
        Dispatcher.BeginInvoke(new Action(() => ApplyPowerMode(mode)), DispatcherPriority.Send);
    }

    private void ApplyPowerMode(PowerModes mode)
    {
        if (_mediaObservationStopped) return;

        if (mode == PowerModes.Suspend)
        {
            ClearWritingActivity();
            StopMediaHeadphoneObservation();
            Interlocked.Exchange(ref _suspended, 1);
            Interlocked.Exchange(ref _lastTick, Stopwatch.GetTimestamp());
            _mediaHeadphonePolicy.Reset();
            _mediaHeadphonesVisible = false;
            RenderPresentation();
        }
        else if (mode == PowerModes.Resume)
        {
            ClearWritingActivity();
            Interlocked.Exchange(ref _lastTick, Stopwatch.GetTimestamp());
            Interlocked.Exchange(ref _suspended, 0);
            _mediaHeadphonePolicy.Reset();
            _mediaHeadphonesVisible = false;
            RenderPresentation();
            if (_mediaHeadphonesEnabled) StartMediaHeadphoneObservation();
        }
    }

    private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _trackingMouse = true;
        _dragged = false;
        _mouseDownScreen = PointToScreen(e.GetPosition(this));
        _lastPointerScreen = _mouseDownScreen;
        _dragStartPosition = _session.Position;
        _dragRequestedPosition = _dragStartPosition;
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
        MoveDraggedPet(pointer, scale);
    }

    private void MoveDraggedPet(System.Windows.Point pointer, DpiScale scale)
    {
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
            _dragRequestedPosition = new PetPosition(
                _dragStartPosition.X + total.X / scale.DpiScaleX,
                _dragStartPosition.Y + total.Y / scale.DpiScaleY);
        }
        else
        {
            var delta = pointer - _lastPointerScreen;
            _dragRequestedPosition = new PetPosition(
                _dragRequestedPosition.X + delta.X / scale.DpiScaleX,
                _dragRequestedPosition.Y + delta.Y / scale.DpiScaleY);
        }

        _session.MoveTo(_dragRequestedPosition, persist: false);
        _lastPointerScreen = pointer;
        ApplyPlacement();
        RenderPresentation();
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
            if (_focus?.HandlePetClick() != true)
            {
                RunSessionCommand(_session.React);
                _activateNotes();
            }
            else if (_focus.PetActionOverride == PetAction.Sleep)
            {
                _activateNotes();
            }
        }

        _dragged = false;
        RenderPresentation();
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
        RenderPresentation();
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
        var pet = new WpfMenuItem { Header = "Pet" };
        pet.Click += (_, _) => RunSessionCommand(_session.Pet);
        menu.Items.Add(pet);
        _sleepActionMenuItem.Click += (_, _) => RunSessionCommand(_session.Sleep);
        menu.Items.Add(_sleepActionMenuItem);
        _editAreaMenuItem.Header = "Edit roaming area";
        _editAreaMenuItem.Click += (_, _) => OpenAreaEditor();
        menu.Items.Add(_editAreaMenuItem);
        menu.Items.Add(new WpfSeparator());
        // Match the icon gutter reserved by ordinary Fluent menu items.
        var notes = new WpfMenuItem { Header = "_Notes", Padding = new Thickness(26, 0, 0, 0) };
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
        var focus = new WpfMenuItem { Header = "_Focus" };
        focus.Click += (_, _) => FocusRequested?.Invoke();
        menu.Items.Add(focus);
        menu.Items.Add(new WpfSeparator());
        var settings = new WpfMenuItem { Header = "Settings" };
        _showSpritesMenuItem.Click += (_, _) =>
        {
            ToggleSpritesRequested?.Invoke();
            UpdateNeedLabels();
        };
        menu.Items.Add(_showSpritesMenuItem);
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
        _showSpritesMenuItem.IsChecked = _session.Configuration.Behavior.ShowSprites;
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

        new WindowInteropHelper(this).EnsureHandle();
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
        if (!_session.Configuration.Behavior.ShowSprites || _assets is not { } assets)
        {
            HeadphoneBandImage.Source = HeadphoneCupsImage.Source = null;
            return;
        }

        var presentation = CurrentPresentation;
        WindowsDisplayProvider.ApplyPosition(this, presentation.Position);
        var animation = assets.Definition.GetAnimation(presentation.Action);
        var frame = animation.Frames[presentation.FrameIndex];
        var rectangle = assets.GetFrameRectangle(frame);
        if (!ReferenceEquals(_renderedSpriteSheet, assets.SpriteSheet) || _renderedRectangle != rectangle)
        {
            var cropped = new CroppedBitmap(assets.SpriteSheet, rectangle);
            cropped.Freeze();
            PetImage.Source = cropped;
            _renderedSpriteSheet = assets.SpriteSheet;
            _renderedRectangle = rectangle;
        }

        if (!_mediaHeadphonesEnabled || !_mediaHeadphonesVisible)
        {
            HeadphoneBandImage.Source = HeadphoneCupsImage.Source = null;
            return;
        }

        var headphoneArt = MediaHeadphoneArtwork.Get(
            assets.Definition.Name, presentation.Action, frame);
        if (!ReferenceEquals(HeadphoneBandImage.Source, headphoneArt.Behind)) HeadphoneBandImage.Source = headphoneArt.Behind;
        if (!ReferenceEquals(HeadphoneCupsImage.Source, headphoneArt.InFront)) HeadphoneCupsImage.Source = headphoneArt.InFront;
    }

    internal void ApplyMediaHeadphones(bool enabled)
    {
        if (_mediaObservationStopped) return;
        if (_mediaHeadphonesEnabled == enabled)
        {
            if (enabled) StartMediaHeadphoneObservation();
            return;
        }

        _mediaHeadphonesEnabled = enabled;
        if (enabled) StartMediaHeadphoneObservation();
        else StopMediaHeadphoneObservation();

        _mediaHeadphonePolicy.Reset();
        _mediaHeadphonesVisible = false;
        RenderPresentation();
    }

    private void StartMediaHeadphoneObservation()
    {
        if (_mediaHeadphoneObserver is not null) return;
        var generation = ++_mediaObserverGeneration;
        _mediaHeadphoneObserver = new MediaHeadphoneObserver(Dispatcher, _mediaAudioProbe,
            (observation, elapsed) =>
            {
                if (generation != _mediaObserverGeneration || !_mediaHeadphonesEnabled) return;
                ApplyMediaAudioObservation(observation, elapsed);
            });
        _mediaHeadphoneObserver.Start();
    }

    private void StopMediaHeadphoneObservation()
    {
        ++_mediaObserverGeneration;
        _mediaHeadphoneObserver?.Dispose();
        _mediaHeadphoneObserver = null;
    }

    internal void ApplyMediaAudioObservation(MediaAudioObservation observation, TimeSpan elapsed)
    {
        var visible = _mediaHeadphonePolicy.Update(
            observation, elapsed, _mediaHeadphonesEnabled && Volatile.Read(ref _suspended) == 0);
        if (_mediaHeadphonesVisible == visible) return;
        _mediaHeadphonesVisible = visible;
        RenderPresentation();
    }

    internal void ApplyAlwaysOnTop(bool enabled) => Topmost = enabled;

    internal LoadedHoodAssets? CurrentAssets => _assets;
    internal HoodAssetSource CurrentSource => _assetSource;
    internal double PetScale => _session.PetScale;
    internal PetPosition PetPosition => _session.Position;
    internal PetBounds RoamingArea => _session.RoamingArea;

    internal void ApplyPetScale(double scale, PetPosition? position = null, PetBounds? roamingArea = null,
        bool loadArtwork = true)
    {
        HoodConfigurationValidator.ValidatePetScale(scale);
        LoadedHoodAssets? assets = null;
        if (loadArtwork && _assets is { } current && scale > current.DecodeScale)
        {
            // Decode once at the slider's maximum to avoid reloading the atlas on every drag step.
            assets = HoodAssets.LoadForDisplay(_assetSource, current.DpiScaleX, current.DpiScaleY, 2);
        }
        _session.SetPetScale(scale, position, roamingArea);
        if (assets is not null) ApplyPetAssets(_assetSource, assets);
        ApplyPlacement();
        RenderPresentation();
    }

    internal LoadedHoodAssets LoadPetAssets(string pet)
    {
        var dpiScale = WindowsDisplayProvider.GetScaleForSize(
            _session.Position, _session.PetWidth, _session.PetHeight, WindowsDisplayProvider.GetDisplays());
        var source = HoodAssets.LoadPetSource(
            System.IO.Path.Combine(AppContext.BaseDirectory, "Assets"), pet, _assetSource.Definition);
        return HoodAssets.LoadForDisplay(source, dpiScale, dpiScale, _session.PetScale);
    }

    internal void ApplyPetAssets(LoadedHoodAssets assets)
    {
        ApplyPetAssets(assets.Source, assets);
    }

    internal void ApplyPetAssets(HoodAssetSource source, LoadedHoodAssets assets)
    {
        _assetSource = source;
        _assets = assets;
        _renderedSpriteSheet = null;
        _renderedRectangle = null;
        RenderPresentation();
    }

    internal void RestorePetAssets(HoodAssetSource source, LoadedHoodAssets? assets)
    {
        if (!_session.Configuration.Behavior.ShowSprites) assets = null;
        else if (assets is null)
        {
            var dpiScale = WindowsDisplayProvider.GetScaleForSize(
                _session.Position, _session.PetWidth, _session.PetHeight, WindowsDisplayProvider.GetDisplays());
            if (source.CachedSpriteSheet is null)
                source = HoodAssets.ReadSourceForPet(source.Directory, source.Definition);
            assets = HoodAssets.LoadForDisplay(source, dpiScale, dpiScale, _session.PetScale);
        }
        _assetSource = source;
        _assets = assets;
        _renderedSpriteSheet = null;
        _renderedRectangle = null;
        PetImage.Source = null;
        RenderPresentation();
    }

    internal void RestorePetAssets(HoodAssetRollbackSnapshot snapshot)
    {
        if (!_session.Configuration.Behavior.ShowSprites)
        {
            RestorePetAssets(snapshot.GetRestoreSource(), null);
            return;
        }
        var dpiScale = WindowsDisplayProvider.GetScaleForSize(
            _session.Position, _session.PetWidth, _session.PetHeight, WindowsDisplayProvider.GetDisplays());
        var (source, assets) = snapshot.LoadForDisplay(dpiScale, _session.PetScale);
        RestorePetAssets(source, assets);
    }

    internal void ReleaseSpriteAssets()
    {
        PetImage.Source = null;
        HeadphoneBandImage.Source = HeadphoneCupsImage.Source = null;
        _renderedSpriteSheet = null;
        _renderedRectangle = null;
        _assets = null;
    }

    internal System.Windows.Point PresentationCenter => new(Left + ActualWidth / 2, Top + ActualHeight / 2);

    internal void AttachFocus(FocusSession focus)
    {
        if (_focus is not null) _focus.Changed -= SyncFocusPresentation;
        _focus = focus;
        focus.Changed += SyncFocusPresentation;
        SyncFocusPresentation();
    }

    private void SyncFocusPresentation()
    {
        var action = _focus?.PetActionOverride;
        if (action == _focusAction) return;
        _focusAction = action;
        _focusAnimationTicks = 0;
        _session.SetFocusPresentation(action is not null);
        RenderPresentation();
        UpdateTimerInterval();
    }

    internal void AdvanceFocusAnimation(TimeSpan elapsed)
    {
        if (_focusAction is not { } action) return;
        var animation = _assetSource.Definition.GetAnimation(action);
        var cycle = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds).Ticks * animation.Frames.Count;
        _focusAnimationTicks = (_focusAnimationTicks + elapsed.Ticks % cycle) % cycle;
    }

    internal void CelebrateChecklistCompletion()
    {
        if (!_session.Configuration.Behavior.ShowSprites) return;
        _checklistAnimationTicks = 0;
        _checklistAnimationStartedAt = Stopwatch.GetTimestamp();
        RenderPresentation();
        UpdateTimerInterval();
    }

    internal void AdvanceChecklistAnimation(TimeSpan elapsed)
    {
        if (_checklistAnimationTicks is not { } ticks) return;
        var animation = _assetSource.Definition.GetAnimation(PetAction.Pet);
        var duration = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds).Ticks * animation.Frames.Count;
        _checklistAnimationTicks = elapsed.Ticks >= duration - ticks ? null : ticks + elapsed.Ticks;
    }

    internal HoodPresentation CurrentPresentation
    {
        get
        {
            if (_checklistAnimationTicks is { } ticks)
            {
                var celebration = _assetSource.Definition.GetAnimation(PetAction.Pet);
                var frameTicks = TimeSpan.FromMilliseconds(celebration.FrameDurationMilliseconds).Ticks;
                return new(PetAction.Pet, (int)(ticks / frameTicks), _session.Position);
            }
            if (_focusAction is not { } action) return WritingPresentation ?? _session.Presentation;
            var animation = _assetSource.Definition.GetAnimation(action);
            var duration = TimeSpan.FromMilliseconds(animation.FrameDurationMilliseconds);
            return new(action, (int)(_focusAnimationTicks / duration.Ticks % animation.Frames.Count), _session.Position);
        }
    }

    internal void RefreshBehaviorSettings()
    {
        UpdateWritingPresentation();
        ApplyMediaHeadphones(_session.Configuration.Behavior.MediaHeadphones);
        ApplyPlacement();
        UpdateTimerInterval();
        RenderPresentation();
        UpdateNeedLabels();
        if (_session.Configuration.Behavior.ShowSprites)
        {
            if (_assets is null)
                throw new InvalidOperationException("Sprite assets must be loaded before the pet is revealed.");
            if (!IsVisible) Show();
            Interlocked.Exchange(ref _lastTick, Stopwatch.GetTimestamp());
            if (IsLoaded) _frameTimer.Start();
        }
        else
        {
            _frameTimer.Stop();
            _areaEditor?.Close();
            Hide();
            ReleaseSpriteAssets();
        }
        SpritesVisibilityChanged?.Invoke();
    }

    internal void ApplyPlacement()
    {
        Width = PetImage.Width = HeadphoneBandImage.Width = HeadphoneCupsImage.Width = _session.PetWidth;
        Height = PetImage.Height = HeadphoneBandImage.Height = HeadphoneCupsImage.Height = _session.PetHeight;
        WindowsDisplayProvider.ApplyPosition(this, CurrentPresentation.Position);
        _areaEditor?.ApplyPlacement();
    }

    private void UpdateTimerInterval()
    {
        var interval = TimeSpan.FromMilliseconds(
            _writingOwner is not null || _checklistAnimationTicks is not null || _focusAction is not null || _session.RequiresAnimationFrames ? 33 : 1000);
        if (_frameTimer.Interval != interval) _frameTimer.Interval = interval;
    }

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
