using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class SettingsWindow : Window
{
    private const string BehaviorSetting = "behavior";
    private readonly HoodConfigurationController _controller;
    private readonly HoodOverlayWindow _petWindow;

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow)
    {
        _controller = controller;
        _petWindow = petWindow;
        InitializeComponent();
        RoamingCheckBox.IsChecked = controller.Current.Behavior.Roaming;
        IdleAnimationsCheckBox.IsChecked = controller.Current.Behavior.IdleAnimations;
        RandomEventsCheckBox.IsChecked = controller.Current.Behavior.RandomEvents;
        StartWithWindowsCheckBox.IsChecked = controller.Current.Behavior.StartWithWindows;
        AlwaysOnTopCheckBox.IsChecked = controller.Current.Behavior.AlwaysOnTop;
        MinimumReactionMinutesTextBox.Text = controller.Current.RandomInterval.MinimumMinutes.ToString(CultureInfo.CurrentCulture);
        MaximumReactionMinutesTextBox.Text = controller.Current.RandomInterval.MaximumMinutes.ToString(CultureInfo.CurrentCulture);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var previous = _controller.Current;
        if (!double.TryParse(MinimumReactionMinutesTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var minimum) ||
            !double.TryParse(MaximumReactionMinutesTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var maximum) ||
            !double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum <= 0 || maximum < minimum)
        {
            System.Windows.MessageBox.Show(this,
                "Enter positive numbers of minutes, with the minimum no greater than the maximum.",
                "Invalid reaction timing", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var candidate = previous with
        {
            RandomInterval = new RandomIntervalSettings { MinimumMinutes = minimum, MaximumMinutes = maximum },
            Behavior = previous.Behavior with
            {
                Roaming = RoamingCheckBox.IsChecked == true,
                IdleAnimations = IdleAnimationsCheckBox.IsChecked == true,
                RandomEvents = RandomEventsCheckBox.IsChecked == true,
                StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
                AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true
            }
        };

        try
        {
            _petWindow.ApplyAlwaysOnTop(candidate.Behavior.AlwaysOnTop);
            _controller.SaveAndApply(BehaviorSetting, candidate);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            RoamingCheckBox.IsChecked = previous.Behavior.Roaming;
            IdleAnimationsCheckBox.IsChecked = previous.Behavior.IdleAnimations;
            RandomEventsCheckBox.IsChecked = previous.Behavior.RandomEvents;
            StartWithWindowsCheckBox.IsChecked = previous.Behavior.StartWithWindows;
            AlwaysOnTopCheckBox.IsChecked = previous.Behavior.AlwaysOnTop;
            MinimumReactionMinutesTextBox.Text = previous.RandomInterval.MinimumMinutes.ToString(CultureInfo.CurrentCulture);
            MaximumReactionMinutesTextBox.Text = previous.RandomInterval.MaximumMinutes.ToString(CultureInfo.CurrentCulture);
            var errorMessage = exception.Message;
            try
            {
                _petWindow.ApplyAlwaysOnTop(previous.Behavior.AlwaysOnTop);
            }
            catch (Exception restoreException) when (restoreException is not OutOfMemoryException)
            {
                errorMessage = $"{errorMessage} Always-on-top could not be restored: {restoreException.Message}";
            }

            System.Windows.MessageBox.Show(
                this,
                $"The settings were not applied. Hood's previous configuration remains in effect where integrations could be restored. {errorMessage}",
                "Settings were not saved",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _petWindow.RefreshBehaviorSettings();
        Close();
    }

    private void OnOpenConfigFolderClick(object sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(new YamlConfigurationStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HoodDesktopPet",
            "config.yaml")).Path)!;
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void OnEditAreaClick(object sender, RoutedEventArgs e) => _petWindow.OpenRoamingAreaEditor();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
