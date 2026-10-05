using System.Diagnostics;
using System.ComponentModel;
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
    private readonly Action<string> _applyUiStyle;
    private readonly string _originalUiStyle;
    private readonly string _originalPet;
    private readonly LoadedHoodAssets _originalAssets;
    private string _effectiveUiStyle;
    private string _previewPet;
    private string _theme = "system";
    private bool _saved;
    private bool _loadingSettings = true;

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow)
        : this(controller, petWindow, controller.Current.UiStyle)
    {
    }

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow, string effectiveUiStyle)
        : this(controller, petWindow, effectiveUiStyle, style => petWindow.ApplyAppearance(controller.Current.Notes.Theme, style))
    {
    }

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow, string effectiveUiStyle, Action<string> applyUiStyle)
    {
        _controller = controller;
        _petWindow = petWindow;
        _originalUiStyle = _effectiveUiStyle = UiStyleCatalog.Canonicalize(effectiveUiStyle);
        _originalPet = _previewPet = controller.Current.Pet;
        _originalAssets = petWindow.CurrentAssets;
        _applyUiStyle = applyUiStyle;
        InitializeComponent();
        ApplyTheme(controller.Current.Notes.Theme, _effectiveUiStyle);
        LoadSettings(controller.Current);
    }

    public void ApplyTheme(string theme, string? style = null)
    {
        if (style is not null)
        {
            _effectiveUiStyle = UiStyleCatalog.Canonicalize(style);
        }

        _theme = theme;
        NotesAppearance.Apply(this, theme, _effectiveUiStyle);
    }

    private void OnUiStyleSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingSettings || UiStyleComboBox.SelectedValue is not string style)
        {
            return;
        }
        try
        {
            PreviewUiStyle(style);
            AppearanceErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _loadingSettings = true;
            UiStyleComboBox.SelectedValue = _effectiveUiStyle;
            _loadingSettings = false;
            ShowAppearanceError($"Could not preview this UI style. Settings were not saved. {exception.Message}");
        }
    }

    private void OnPetSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingSettings || PetComboBox.SelectedValue is not string pet)
        {
            return;
        }
        try
        {
            PreviewPet(pet);
            AppearanceErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _loadingSettings = true;
            PetComboBox.SelectedValue = _previewPet;
            _loadingSettings = false;
            ShowAppearanceError($"Could not preview this pet. Settings were not saved. {exception.Message}");
        }
    }

    private void PreviewUiStyle(string style)
    {
        if (style == _effectiveUiStyle) return;
        _applyUiStyle(style);
        ApplyTheme(_theme, style);
    }

    private void PreviewPet(string pet)
    {
        if (pet == _previewPet) return;
        var assets = pet == _originalPet ? _originalAssets : _petWindow.LoadPetAssets(pet);
        _petWindow.ApplyPetAssets(assets);
        _previewPet = pet;
    }

    private void RestoreAppearance()
    {
        if (!ReferenceEquals(_petWindow.CurrentAssets, _originalAssets))
        {
            _petWindow.ApplyPetAssets(_originalAssets);
        }
        _previewPet = _originalPet;
        PreviewUiStyle(_originalUiStyle);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_saved || e.Cancel) return;
        try
        {
            RestoreAppearance();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            e.Cancel = true;
            ShowAppearanceError($"Could not restore the previous appearance. Try closing Settings again. {exception.Message}");
        }
    }

    private void LoadSettings(HoodConfiguration configuration)
    {
        _loadingSettings = true;
        PetComboBox.SelectedValue = configuration.Pet;
        UiStyleComboBox.SelectedValue = configuration.UiStyle;
        RoamingCheckBox.IsChecked = configuration.Behavior.Roaming;
        IdleAnimationsCheckBox.IsChecked = configuration.Behavior.IdleAnimations;
        RandomEventsCheckBox.IsChecked = configuration.Behavior.RandomEvents;
        StartWithWindowsCheckBox.IsChecked = configuration.Behavior.StartWithWindows;
        AlwaysOnTopCheckBox.IsChecked = configuration.Behavior.AlwaysOnTop;
        MinimumReactionMinutesTextBox.Text = configuration.RandomInterval.MinimumMinutes.ToString(CultureInfo.CurrentCulture);
        MaximumReactionMinutesTextBox.Text = configuration.RandomInterval.MaximumMinutes.ToString(CultureInfo.CurrentCulture);
        _loadingSettings = false;
    }

    private void ShowAppearanceError(string message)
    {
        AppearanceErrorText.Text = message;
        AppearanceErrorText.Visibility = Visibility.Visible;
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
            Pet = PetComboBox.SelectedValue as string ?? PetCatalog.Hood,
            UiStyle = UiStyleComboBox.SelectedValue as string ?? UiStyleCatalog.StickyNotes,
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
            PreviewPet(candidate.Pet);
            PreviewUiStyle(candidate.UiStyle);
            _petWindow.ApplyAlwaysOnTop(candidate.Behavior.AlwaysOnTop);
            _controller.SaveAndApply(BehaviorSetting, candidate);
            _saved = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LoadSettings(previous);
            var errorMessage = exception.Message;
            try
            {
                _petWindow.ApplyAlwaysOnTop(previous.Behavior.AlwaysOnTop);
                RestoreAppearance();
            }
            catch (Exception restoreException) when (restoreException is not OutOfMemoryException)
            {
                errorMessage = $"{errorMessage} The previous appearance could not be fully restored: {restoreException.Message}";
            }

            ShowAppearanceError($"Settings were not saved. Hood's previous configuration remains in effect where integrations could be restored. {errorMessage}");
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
