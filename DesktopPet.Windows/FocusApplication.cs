using System.ComponentModel;
using System.Diagnostics;
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
    private readonly Func<string?> _openSettings;
    private readonly Func<Window, bool> _activateWindow;
    private readonly Action _playCompletionSound;
    private long _lastTick;
    private FocusWindow? _window;
    private bool _topmost;
    private bool _disposed;
    private string _theme;
    private string _style;
    internal FocusSession Session { get; } = new();

    internal FocusApplication(Dispatcher dispatcher, string theme, string style, Func<long>? timestamp = null,
        Func<string?>? openSettings = null, Func<Window, bool>? activateWindow = null, Action? playCompletionSound = null)
    {
        _dispatcher = dispatcher;
        _theme = theme;
        _style = style;
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
        _openSettings = openSettings ?? OpenNotificationSettings;
        _activateWindow = activateWindow ?? (window => window.Activate());
        _playCompletionSound = playCompletionSound ?? System.Media.SystemSounds.Asterisk.Play;
        _lastTick = _timestamp();
        _timer.Tick += OnTick;
        Session.RestoreRequested += Show;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _timer.Start();
    }

    internal void Show()
    {
        if (_disposed) return;
        if (_window is null)
        {
            _window = new FocusWindow(Session, Execute, _topmost, value => _topmost = value, _openSettings);
            _window.ApplyTheme(_theme, _style);
            _window.Closed += (_, _) => { Session.Stop(); _window = null; };
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
    }

    private static string? OpenNotificationSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true });
            return null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return $"Could not open Windows notification settings. Open Settings > System > Notifications manually. {exception.Message}";
        }
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
    }
}
