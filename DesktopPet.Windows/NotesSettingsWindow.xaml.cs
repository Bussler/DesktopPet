using System.Windows;
using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class NotesSettingsWindow : Window
{
    private readonly HoodConfigurationController _controller;
    private readonly Action _applySettings;
    private string _uiStyle;
    private NotesConfiguration _effectiveSettings;

    internal NotesSettingsWindow(HoodConfigurationController controller, Action applySettings)
        : this(controller, applySettings, UiStyleCatalog.StickyNotes, controller.Current.Notes)
    {
    }

    internal NotesSettingsWindow(HoodConfigurationController controller, Action applySettings, string uiStyle)
        : this(controller, applySettings, uiStyle, controller.Current.Notes)
    {
    }

    internal NotesSettingsWindow(HoodConfigurationController controller, Action applySettings, string uiStyle, NotesConfiguration effectiveSettings)
    {
        _controller = controller;
        _applySettings = applySettings;
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        _effectiveSettings = effectiveSettings;
        InitializeComponent();
        ApplyEffectiveSettings(_effectiveSettings, _uiStyle);
        DefaultColorComboBox.ItemsSource = NoteColorPalette.Colors;
        ThemeComboBox.ItemsSource = Enum.GetValues<NoteTheme>();
        LoadSettings(controller.Current.Notes);
    }

    public void ApplyTheme(string theme, string? style = null)
    {
        if (style is not null)
        {
            _uiStyle = UiStyleCatalog.Canonicalize(style);
        }

        NotesAppearance.Apply(this, theme, _uiStyle);
    }

    public void ApplyEffectiveSettings(NotesConfiguration settings, string? style = null)
    {
        _effectiveSettings = settings;
        ApplyTheme(settings.Theme, style);
        UpdateEffectiveSettingsNotice();
    }

    private void UpdateEffectiveSettingsNotice()
    {
        var pending = _controller.Current.Notes != _effectiveSettings;
        AppearanceNoticeText.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        if (pending)
        {
            AppearanceNoticeText.Text = "Some saved Notes settings are not active in this session. Restart Hood to apply them.";
        }
    }

    private void LoadSettings(NotesConfiguration settings)
    {
        DefaultColorComboBox.SelectedItem = NoteColorPalette.Parse(settings.DefaultColor);
        ThemeComboBox.SelectedItem = Enum.Parse<NoteTheme>(settings.Theme, ignoreCase: true);
        TextSizeSlider.Value = settings.TextSize;
        TextSizeInput.Text = settings.TextSize.ToString();
        AlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
        AnimateShowHideCheckBox.IsChecked = settings.AnimateShowHide;
    }

    private void OnTextSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TextSizeInput is not null)
        {
            TextSizeInput.Text = $"{(int)e.NewValue}";
        }
    }

    private void OnTextSizeInputChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (TextSizeSlider is not null && int.TryParse(TextSizeInput.Text, out var size)
            && size >= TextSizeSlider.Minimum && size <= TextSizeSlider.Maximum)
        {
            TextSizeSlider.Value = size;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var previous = _controller.Current;
        if (!int.TryParse(TextSizeInput.Text, out var textSize)
            || textSize < TextSizeSlider.Minimum || textSize > TextSizeSlider.Maximum)
        {
            ShowError("Enter a whole-number text size from 12 to 32 pt.");
            TextSizeInput.Focus();
            return;
        }
        if (DefaultColorComboBox.SelectedItem is not NoteColor color || ThemeComboBox.SelectedItem is not NoteTheme theme)
        {
            ShowError("Select a supported default color and theme.");
            return;
        }

        var candidate = previous with
        {
            Notes = previous.Notes with
            {
                DefaultColor = color.ToString().ToLowerInvariant(),
                TextSize = textSize,
                Theme = theme.ToString().ToLowerInvariant(),
                AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
                AnimateShowHide = AnimateShowHideCheckBox.IsChecked == true
            }
        };

        try
        {
            _controller.SaveAndApply("notes", candidate);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LoadSettings(previous.Notes);
            ShowError($"The settings were not saved. {exception.Message}");
            return;
        }

        try
        {
            _applySettings();
            Close();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            UpdateEffectiveSettingsNotice();
            ShowError($"The settings were saved, but Hood could not refresh every open surface. Restart Hood to apply them. {exception.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage.Text = message;
        ErrorMessage.Visibility = Visibility.Visible;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
