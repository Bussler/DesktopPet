using System.ComponentModel;
using System.Globalization;
using System.Windows;
using DesktopPet.Core;

namespace DesktopPet.Windows;

public partial class NotesSettingsWindow : Window
{
    private readonly HoodConfigurationController _controller;
    private readonly Action<NotesConfiguration> _applySettings;
    private string _uiStyle;
    private NotesConfiguration _effectiveSettings;
    private NotesConfiguration _originalSettings;
    private bool _saved;
    private bool _loadingSettings = true;

    internal NotesSettingsWindow(HoodConfigurationController controller, Action<NotesConfiguration> applySettings)
        : this(controller, applySettings, UiStyleCatalog.StickyNotes, controller.Current.Notes)
    {
    }

    internal NotesSettingsWindow(HoodConfigurationController controller, Action<NotesConfiguration> applySettings, string uiStyle)
        : this(controller, applySettings, uiStyle, controller.Current.Notes)
    {
    }

    internal NotesSettingsWindow(HoodConfigurationController controller, Action<NotesConfiguration> applySettings, string uiStyle, NotesConfiguration effectiveSettings)
    {
        _controller = controller;
        _applySettings = applySettings;
        _uiStyle = UiStyleCatalog.Canonicalize(uiStyle);
        _originalSettings = _effectiveSettings = effectiveSettings;
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
            AppearanceNoticeText.Text = !_saved && _effectiveSettings != _originalSettings
                ? "Previewing unsaved settings. Save to keep them; Cancel or close to restore the previous settings."
                : "Some saved Notes settings are not active in this session. Restart Hood to apply them.";
        }
    }

    private void LoadSettings(NotesConfiguration settings)
    {
        _loadingSettings = true;
        DefaultColorComboBox.SelectedItem = NoteColorPalette.Parse(settings.DefaultColor);
        ThemeComboBox.SelectedItem = Enum.Parse<NoteTheme>(settings.Theme, ignoreCase: true);
        TextSizeSlider.Value = settings.TextSize;
        TextSizeInput.Text = settings.TextSize.ToString();
        LineSpacingSlider.Value = settings.LineSpacing;
        LineSpacingInput.Text = settings.LineSpacing.ToString(CultureInfo.CurrentCulture);
        AlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
        AnimateShowHideCheckBox.IsChecked = settings.AnimateShowHide;
        _loadingSettings = false;
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
        OnSettingsChanged(sender, e);
    }

    private bool TryReadSettings(out NotesConfiguration settings, bool showErrors = false)
    {
        settings = _controller.Current.Notes;
        if (!int.TryParse(TextSizeInput.Text, out var textSize)
            || textSize < TextSizeSlider.Minimum || textSize > TextSizeSlider.Maximum)
        {
            if (showErrors)
            {
                ShowError("Enter a whole-number text size from 12 to 32 pt.");
                TextSizeInput.Focus();
            }
            return false;
        }
        if (!double.TryParse(LineSpacingInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var lineSpacing)
            || !double.IsFinite(lineSpacing) || lineSpacing < LineSpacingSlider.Minimum || lineSpacing > LineSpacingSlider.Maximum)
        {
            if (showErrors)
            {
                ShowError("Enter a line-spacing value from 1 to 3 (2 is normal).");
                LineSpacingInput.Focus();
            }
            return false;
        }
        if (DefaultColorComboBox.SelectedItem is not NoteColor color || ThemeComboBox.SelectedItem is not NoteTheme theme)
        {
            if (showErrors) ShowError("Select a supported default color and theme.");
            return false;
        }

        settings = settings with
        {
            DefaultColor = color.ToString().ToLowerInvariant(),
            TextSize = textSize,
            LineSpacing = lineSpacing,
            Theme = theme.ToString().ToLowerInvariant(),
            AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
            AnimateShowHide = AnimateShowHideCheckBox.IsChecked == true
        };
        return true;
    }

    private void PreviewSettings(NotesConfiguration settings)
    {
        if (settings == _effectiveSettings) return;
        _applySettings(settings);
        ApplyEffectiveSettings(settings);
    }

    private void OnSettingsSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => OnSettingsChanged(sender, e);

    private void OnSettingsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings || !TryReadSettings(out var settings) || settings == _effectiveSettings) return;
        _saved = false;
        try
        {
            PreviewSettings(settings);
            ErrorMessage.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LoadSettings(_effectiveSettings);
            ShowError($"Could not preview these Notes settings. Settings were not saved. {exception.Message}");
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_saved || e.Cancel) return;
        try { PreviewSettings(_originalSettings); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            e.Cancel = true;
            ShowError($"Could not restore the previous Notes settings. Try closing Notes settings again. {exception.Message}");
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings, showErrors: true)) return;
        var previous = _controller.Current;

        try
        {
            _controller.SaveAndApply("notes", previous with { Notes = settings });
            _originalSettings = settings;
            _saved = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LoadSettings(previous.Notes);
            var message = exception.Message;
            try { PreviewSettings(_originalSettings); }
            catch (Exception restoreException) when (restoreException is not OutOfMemoryException)
            {
                message += $" The previous Notes settings could not be fully restored: {restoreException.Message}";
            }
            ShowError($"The settings were not saved. {message}");
            return;
        }

        try
        {
            _applySettings(settings);
            ApplyEffectiveSettings(settings);
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

    private void OnLineSpacingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LineSpacingInput is not null)
            LineSpacingInput.Text = e.NewValue.ToString(CultureInfo.CurrentCulture);
    }

    private void OnLineSpacingInputChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (LineSpacingSlider is not null
            && double.TryParse(LineSpacingInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var spacing)
            && double.IsFinite(spacing) && spacing >= LineSpacingSlider.Minimum && spacing <= LineSpacingSlider.Maximum)
            LineSpacingSlider.Value = spacing;
        OnSettingsChanged(sender, e);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
