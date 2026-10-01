using System.Diagnostics;
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
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var previous = _controller.Current;
        var candidate = previous with
        {
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
