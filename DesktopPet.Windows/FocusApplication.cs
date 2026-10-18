using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using DesktopPet.Core;
using Microsoft.Win32;

namespace DesktopPet.Windows;

/// <summary>Owns one transient window and a clock independent of pet visibility/rendering.</summary>
internal sealed class FocusApplication : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Dispatcher _dispatcher;
    private readonly Func<long> _timestamp;
    private readonly Func<Window, bool> _activateWindow;
    private readonly Action _playCompletionSound;
    private readonly SoundPlayer? _completionSound;
    private readonly FocusNotifications _notifications;
    private readonly Action<string> _reportNotificationError;
    private bool _automaticNotifications;
    private long _lastTick;
    private FocusWindow? _window;
    private bool _topmost;
    private bool _disposed;
    private string _theme;
    private string _style;
    internal FocusSession Session { get; } = new();

    internal FocusApplication(Dispatcher dispatcher, string theme, string style, Func<long>? timestamp = null,
        Func<Window, bool>? activateWindow = null, Action? playCompletionSound = null,
        FocusNotifications? notifications = null, Action<string>? reportNotificationError = null)
    {
        _dispatcher = dispatcher;
        _theme = theme;
        _style = style;
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
        _activateWindow = activateWindow ?? (window => window.Activate());
        _notifications = notifications ?? new FocusNotifications();
        _reportNotificationError = reportNotificationError ?? ReportNotificationError;
        if (playCompletionSound is null)
        {
            _completionSound = new SoundPlayer(typeof(FocusApplication).Assembly.GetManifestResourceStream("DesktopPet.FocusCompletion.wav")
                ?? throw new InvalidOperationException("The focus completion sound is missing."));
            _completionSound.Load();
        }
        _playCompletionSound = playCompletionSound ?? _completionSound!.Play;
        _lastTick = _timestamp();
        _timer.Tick += OnTick;
        Session.RestoreRequested += Show;
        Session.Changed += OnFocusChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _timer.Start();
    }

    internal void Show()
    {
        if (_disposed) return;
        if (_window is null)
        {
            _window = new FocusWindow(Session, Execute, _topmost, value => _topmost = value,
                _automaticNotifications, SetAutomaticNotifications);
            _window.ApplyTheme(_theme, _style);
            _window.Closed += (_, _) =>
            {
                Session.Stop();
                UpdateNotifications(force: true);
                _window = null;
                if (!_disposed && _notifications.Error.Length > 0) _reportNotificationError(_notifications.Error);
            };
            _window.SetNotificationError(_notifications.Error);
            _window.Show();
        }
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        if (!_activateWindow(_window))
        {
            // Windows can deny foreground activation. Raise the window without keeping it pinned.
            var topmost = _window.Topmost;
            try { _window.Topmost = !topmost; }
            finally { _window.Topmost = topmost; }
        }
    }

    internal void Execute(Action command)
    {
        Tick();
        command();
    }

    private void SetAutomaticNotifications(bool enabled)
    {
        Tick();
        _automaticNotifications = enabled;
        UpdateNotifications(force: true);
    }

    private void OnFocusChanged() => UpdateNotifications(force: Session.Phase == FocusPhase.Idle);

    private static void ReportNotificationError(string message) =>
        System.Windows.MessageBox.Show(message, "Hood notifications", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void UpdateNotifications(bool force = false)
    {
        if (_notifications.Update(_automaticNotifications && Session.Phase == FocusPhase.Focus && !Session.IsPaused, force))
            _window?.SetNotificationError(_notifications.Error);
    }

    private void OnTick(object? sender, EventArgs e) => Tick();
    internal void Tick()
    {
        var now = _timestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastTick, now);
        _lastTick = now;
        var wasRunning = Session.IsRunning;
        Session.Advance(elapsed);
        if (wasRunning && Session.Phase is FocusPhase.FocusFinished or FocusPhase.BreakFinished)
            _playCompletionSound();
    }

    internal void ApplyTheme(string theme, string style)
    {
        _theme = theme;
        _style = style;
        _window?.ApplyTheme(theme, style);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        // Suspend discards the cycle; resume also resets in case dispatcher delivery was delayed.
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume)
            _dispatcher.BeginInvoke(Reset, DispatcherPriority.Send);
    }

    internal void Reset()
    {
        _lastTick = _timestamp();
        Session.Stop();
        UpdateNotifications(force: true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        Session.RestoreRequested -= Show;
        Reset();
        _window?.Close();
        Session.Changed -= OnFocusChanged;
        if (_notifications.Error.Length > 0) _reportNotificationError(_notifications.Error);
        _completionSound?.Dispose();
    }
}
