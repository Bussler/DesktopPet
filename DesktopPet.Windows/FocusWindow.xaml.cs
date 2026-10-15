using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class FocusWindow : Window
{
    private readonly FocusSession _session;
    private readonly Action<Action> _execute;
    private readonly Func<string?> _openNotificationSettings;
    private readonly Action<bool> _setTopmost;
    private bool _loading = true;

    internal FocusWindow(FocusSession session, Action<Action> execute, bool topmost, Action<bool> setTopmost,
        Func<string?> openNotificationSettings)
    {
        _session = session;
        _execute = execute;
        _setTopmost = setTopmost;
        _openNotificationSettings = openNotificationSettings;
        InitializeComponent();
        FocusInput.Text = session.FocusMinutes.ToString(CultureInfo.CurrentCulture);
        BreakInput.Text = session.BreakMinutes.ToString(CultureInfo.CurrentCulture);
        TopmostCheckBox.IsChecked = Topmost = topmost;
        _loading = false;
        session.Changed += Refresh;
        Closed += (_, _) => session.Changed -= Refresh;
        Refresh();
    }

    public void ApplyTheme(string theme, string style) => NotesAppearance.Apply(this, theme, style);

    private bool ValidateDurations()
    {
        if (BreakInput is null || ValidationText is null) return false;
        var valid = int.TryParse(FocusInput.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var focus) &&
            int.TryParse(BreakInput.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var rest) &&
            focus is >= 1 and <= FocusSession.MaximumMinutes && rest is >= 1 and <= FocusSession.MaximumMinutes;
        ValidationText.Text = valid ? "" : $"Enter whole minutes from 1 to {FocusSession.MaximumMinutes} for both durations.";
        return valid;
    }

    private void OnDurationChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (StartButton is null || !ValidateDurations())
        {
            if (StartButton is not null) StartButton.IsEnabled = BreakButton.IsEnabled = false;
            return;
        }
        _session.Configure(int.Parse(FocusInput.Text, CultureInfo.CurrentCulture), int.Parse(BreakInput.Text, CultureInfo.CurrentCulture));
    }

    private void Refresh()
    {
        PhaseText.Text = _session.Phase switch
        {
            FocusPhase.Focus => _session.IsPaused ? "Focus paused" : "Focus time",
            FocusPhase.FocusFinished => "Focus finished",
            FocusPhase.Break => _session.IsPaused ? "Break paused" : "Break time",
            FocusPhase.BreakFinished => "Break finished",
            _ => "Ready to focus"
        };
        var remaining = _session.Phase == FocusPhase.Idle ? TimeSpan.FromMinutes(_session.FocusMinutes) : _session.Remaining;
        var seconds = (long)Math.Ceiling(remaining.TotalSeconds);
        CountdownText.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        var valid = ValidateDurations();
        StartButton.IsEnabled = valid && _session.CanStartFocus;
        BreakButton.IsEnabled = valid && _session.Phase == FocusPhase.FocusFinished;
        PauseButton.IsEnabled = _session.Phase is FocusPhase.Focus or FocusPhase.Break;
        PauseButton.Content = _session.IsPaused ? "_Resume" : "_Pause";
        StopButton.IsEnabled = _session.Phase != FocusPhase.Idle;
        SkipButton.IsEnabled = _session.Phase == FocusPhase.Break;
    }

    private void OnStart(object sender, RoutedEventArgs e) => _execute(_session.StartFocus);
    private void OnBreak(object sender, RoutedEventArgs e) => _execute(_session.TakeBreak);
    private void OnPause(object sender, RoutedEventArgs e) => _execute(_session.TogglePause);
    private void OnStop(object sender, RoutedEventArgs e) => _execute(_session.Stop);
    private void OnSkip(object sender, RoutedEventArgs e) => _execute(_session.SkipBreak);
    private void OnTopmostChanged(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostCheckBox.IsChecked == true;
        _setTopmost(Topmost);
    }
    private void OnNotificationSettings(object sender, RoutedEventArgs e) => NotificationErrorText.Text = _openNotificationSettings() ?? "";
}
