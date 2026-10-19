using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class SettingsWindow : Window
{
    private const string BehaviorSetting = "behavior";
    private readonly HoodConfigurationController _controller;
    private readonly HoodOverlayWindow _petWindow;
    private readonly Action<string> _applyUiStyle;
    private readonly Action<string> _applyTheme;
    private readonly string _originalUiStyle;
    private readonly string _originalTheme;
    private readonly string _originalPet;
    private readonly double _originalPetScale;
    private PetPosition _restorePetPosition;
    private PetBounds _restoreRoamingArea;
    private PetPosition _previewPetPosition;
    private PetBounds _previewRoamingArea;
    private bool _sizePreviewed;
    private readonly HoodAssetSource _originalSource;
    private HoodAssetRollbackSnapshot? _originalAssetSnapshot;
    private LoadedHoodAssets? _originalAssets;
    private string _effectiveUiStyle;
    private string _previewPet;
    private string _theme = "system";
    private bool _saved;
    private bool _loadingSettings = true;

    internal string ConfigurationDirectory { get; }

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow)
        : this(controller, petWindow, controller.Current.UiStyle)
    {
    }

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow, string effectiveUiStyle)
        : this(controller, petWindow, effectiveUiStyle, null, controller.Current.Notes.Theme, null)
    {
    }

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow, string effectiveUiStyle, Action<string> applyUiStyle)
        : this(controller, petWindow, effectiveUiStyle, applyUiStyle, controller.Current.Notes.Theme, null)
    {
    }

    internal SettingsWindow(HoodConfigurationController controller, HoodOverlayWindow petWindow, string effectiveUiStyle,
        Action<string>? applyUiStyle, string effectiveTheme, Action<string>? applyTheme, string? configurationDirectory = null)
    {
        _controller = controller;
        _petWindow = petWindow;
        ConfigurationDirectory = configurationDirectory ?? HoodDataDirectory.Resolve();
        _originalUiStyle = _effectiveUiStyle = UiStyleCatalog.Canonicalize(effectiveUiStyle);
        _originalTheme = _theme = effectiveTheme;
        _originalPet = _previewPet = controller.Current.Pet;
        _originalPetScale = petWindow.PetScale;
        _restorePetPosition = _previewPetPosition = petWindow.PetPosition;
        _restoreRoamingArea = _previewRoamingArea = petWindow.RoamingArea;
        var originalAssets = petWindow.CurrentAssets;
        _originalSource = petWindow.CurrentSource;
        _originalAssets = originalAssets;
        _applyUiStyle = applyUiStyle ?? (style => petWindow.ApplyAppearance(_theme, style));
        _applyTheme = applyTheme ?? (theme => petWindow.ApplyAppearance(theme, _effectiveUiStyle));
        InitializeComponent();
        ThemeComboBox.ItemsSource = Enum.GetValues<NoteTheme>();
        ApplyTheme(_theme, _effectiveUiStyle);
        LoadSettings(controller.Current);
    }

    public void ApplyTheme(string theme, string? style = null)
    {
        if (style is not null)
        {
            _effectiveUiStyle = UiStyleCatalog.Canonicalize(style);
        }

        _theme = theme;
        var loading = _loadingSettings;
        _loadingSettings = true;
        ThemeComboBox.SelectedItem = Enum.Parse<NoteTheme>(theme, ignoreCase: true);
        _loadingSettings = loading;
        NotesAppearance.Apply(this, theme, _effectiveUiStyle);
    }

    private void OnThemeSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingSettings || ThemeComboBox.SelectedItem is not NoteTheme theme) return;
        try
        {
            PreviewTheme(theme.ToString().ToLowerInvariant());
            AppearanceErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ApplyTheme(_theme);
            ShowAppearanceError($"Could not preview this theme. Settings were not saved. {exception.Message}");
        }
    }

    private void PreviewTheme(string theme)
    {
        if (theme == _theme) return;
        _applyTheme(theme);
        ApplyTheme(theme);
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

    private void OnPetSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingSettings) return;
        try
        {
            TrackPlacementChanges();
            CaptureOriginalAssetsForRollback();
            _petWindow.ApplyPetScale(e.NewValue / 100);
            RememberPreviewPlacement();
            _sizePreviewed = true;
            AppearanceErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _loadingSettings = true;
            PetSizeSlider.Value = _petWindow.PetScale * 100;
            _loadingSettings = false;
            ShowAppearanceError($"Could not preview this pet size. Settings were not saved. {exception.Message}");
        }
    }

    private void PreviewPet(string pet)
    {
        if (pet == _previewPet) return;
        if (pet == _originalPet)
            RestoreOriginalPetAssets();
        else
        {
            CaptureOriginalAssetsForRollback();
            _petWindow.ApplyPetAssets(_petWindow.LoadPetAssets(pet));
        }
        _previewPet = pet;
    }

    private void CaptureOriginalAssetsForRollback()
    {
        if (_originalAssets is null || _originalAssetSnapshot is not null) return;
        try
        {
            _originalAssetSnapshot = HoodAssetRollbackSnapshot.Capture(_originalSource, _originalAssets);
            _originalAssets = null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Keep the decoded original if a compressed rollback snapshot cannot be captured.
        }
    }

    private void RestoreAppearance()
    {
        if (_sizePreviewed)
        {
            TrackPlacementChanges();
            // Original artwork is restored below; rollback must not reload the preview's source.
            _petWindow.ApplyPetScale(_originalPetScale, _restorePetPosition, _restoreRoamingArea, loadArtwork: false);
            RememberPreviewPlacement();
        }
        if (_sizePreviewed || _previewPet != _originalPet || _originalAssetSnapshot?.SourceFilesChanged == true)
            RestoreOriginalPetAssets();
        _sizePreviewed = false;
        _previewPet = _originalPet;
        PreviewUiStyle(_originalUiStyle);
        PreviewTheme(_originalTheme);
    }

    private void TrackPlacementChanges()
    {
        var position = _petWindow.PetPosition;
        var area = _petWindow.RoamingArea;
        // Carry intervening movement and explicit area dimensions into the rollback placement.
        _restorePetPosition = new PetPosition(
            _restorePetPosition.X + (position.X - _previewPetPosition.X),
            _restorePetPosition.Y + (position.Y - _previewPetPosition.Y));
        _restoreRoamingArea = _restoreRoamingArea with
        {
            X = area.Width == _previewRoamingArea.Width
                ? _restoreRoamingArea.X + (area.X - _previewRoamingArea.X) : area.X,
            Y = area.Height == _previewRoamingArea.Height
                ? _restoreRoamingArea.Y + (area.Y - _previewRoamingArea.Y) : area.Y,
            Width = area.Width == _previewRoamingArea.Width ? _restoreRoamingArea.Width : area.Width,
            Height = area.Height == _previewRoamingArea.Height ? _restoreRoamingArea.Height : area.Height
        };
        RememberPreviewPlacement();
    }

    private void RememberPreviewPlacement()
    {
        _previewPetPosition = _petWindow.PetPosition;
        _previewRoamingArea = _petWindow.RoamingArea;
    }

    private void RestoreOriginalPetAssets()
    {
        if (_originalAssetSnapshot is null)
            _petWindow.RestorePetAssets(_originalSource, _originalAssets);
        else
            _petWindow.RestorePetAssets(_originalAssetSnapshot);
        _previewPet = _originalPet;
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
        PetSizeSlider.Value = configuration.PetScale * 100;
        UiStyleComboBox.SelectedValue = configuration.UiStyle;
        ThemeComboBox.SelectedItem = Enum.Parse<NoteTheme>(_theme, ignoreCase: true);
        ShowSpritesCheckBox.IsChecked = configuration.Behavior.ShowSprites;
        RoamingCheckBox.IsChecked = configuration.Behavior.Roaming;
        IdleAnimationsCheckBox.IsChecked = configuration.Behavior.IdleAnimations;
        WritingAnimationsCheckBox.IsChecked = configuration.Behavior.WritingAnimations;
        RandomEventsCheckBox.IsChecked = configuration.Behavior.RandomEvents;
        MediaHeadphonesCheckBox.IsChecked = configuration.Behavior.MediaHeadphones;
        StartWithWindowsCheckBox.IsChecked = configuration.Behavior.StartWithWindows;
        AlwaysOnTopCheckBox.IsChecked = configuration.Behavior.AlwaysOnTop;
        MinimumReactionMinutesTextBox.Text = configuration.RandomInterval.MinimumMinutes.ToString(CultureInfo.CurrentCulture);
        MaximumReactionMinutesTextBox.Text = configuration.RandomInterval.MaximumMinutes.ToString(CultureInfo.CurrentCulture);
        _loadingSettings = false;
    }

    internal void RefreshSpriteVisibility() => ShowSpritesCheckBox.IsChecked = _controller.Current.Behavior.ShowSprites;

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
            PetScale = PetSizeSlider.Value / 100,
            UiStyle = UiStyleComboBox.SelectedValue as string ?? UiStyleCatalog.StickyNotes,
            Notes = previous.Notes with { Theme = ((NoteTheme)ThemeComboBox.SelectedItem).ToString().ToLowerInvariant() },
            RandomInterval = new RandomIntervalSettings { MinimumMinutes = minimum, MaximumMinutes = maximum },
            Behavior = previous.Behavior with
            {
                ShowSprites = ShowSpritesCheckBox.IsChecked == true,
                Roaming = RoamingCheckBox.IsChecked == true,
                IdleAnimations = IdleAnimationsCheckBox.IsChecked == true,
                WritingAnimations = WritingAnimationsCheckBox.IsChecked == true,
                RandomEvents = RandomEventsCheckBox.IsChecked == true,
                StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
                AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
                MediaHeadphones = MediaHeadphonesCheckBox.IsChecked == true
            }
        };

        try
        {
            PreviewPet(candidate.Pet);
            _petWindow.ApplyPetScale(candidate.PetScale);
            PreviewUiStyle(candidate.UiStyle);
            PreviewTheme(candidate.Notes.Theme);
            if (candidate.Behavior.ShowSprites) _petWindow.EnsureSpriteAssetsLoaded();
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
        Process.Start(new ProcessStartInfo(ConfigurationDirectory) { UseShellExecute = true });
    }

    private void OnEditAreaClick(object sender, RoutedEventArgs e) => _petWindow.OpenRoamingAreaEditor();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
