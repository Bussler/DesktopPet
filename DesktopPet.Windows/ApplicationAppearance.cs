using System.Windows;
using System.Windows.Media;
using DesktopPet.Core;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;

namespace DesktopPet.Windows;

internal sealed record AppearancePalette(
    MediaColor Window, MediaColor Settings, MediaColor Card, MediaColor Text, MediaColor SecondaryText,
    MediaColor SettingsText, MediaColor SettingsSecondaryText, MediaColor Border,
    MediaColor Hover, MediaColor Selected, MediaColor Error, MediaColor Accent, MediaColor Focus, MediaColor Menu);

internal sealed record AppearanceMetrics(
    CornerRadius WindowRadius, CornerRadius CardRadius, CornerRadius ControlRadius, CornerRadius NoteHeaderRadius,
    CornerRadius CheckboxRadius, Thickness WindowPadding, Thickness SettingsCardPadding,
    Thickness SettingsPanelPadding, Thickness ListCardPadding, Thickness ListCardSpacing, Thickness SectionSpacing);

internal sealed record AppearanceTypography(
    double SettingsHeadingSize, FontWeight SettingsHeadingWeight, double SectionHeadingSize,
    FontWeight SectionHeadingWeight, double NoteTitleSize, FontWeight NoteTitleWeight,
    double ListTitleSize, FontWeight ListTitleWeight, double ListPreviewSize, double ListSecondarySize);

internal static class ApplicationAppearance
{
    private static readonly AppearanceMetrics StickyNotesMetrics = new(
        new CornerRadius(8), new CornerRadius(6), new CornerRadius(4), new CornerRadius(7, 7, 0, 0),
        new CornerRadius(3), new Thickness(20), new Thickness(14, 12, 14, 4), new Thickness(16),
        new Thickness(14), new Thickness(0, 0, 0, 8), new Thickness(0, 0, 0, 14));
    private static readonly AppearanceMetrics MinimalMetrics = new(
        new CornerRadius(4), new CornerRadius(3), new CornerRadius(2), new CornerRadius(3, 3, 0, 0),
        new CornerRadius(2), new Thickness(14), new Thickness(12, 10, 12, 4), new Thickness(12),
        new Thickness(12), new Thickness(0, 0, 0, 6), new Thickness(0, 0, 0, 10));
    private static readonly AppearanceMetrics SoftMetrics = new(
        new CornerRadius(12), new CornerRadius(10), new CornerRadius(8), new CornerRadius(10, 10, 0, 0),
        new CornerRadius(5), new Thickness(22), new Thickness(16, 14, 16, 6), new Thickness(20),
        new Thickness(16), new Thickness(0, 0, 0, 10), new Thickness(0, 0, 0, 18));
    private static readonly AppearanceMetrics StudioMetrics = new(
        new CornerRadius(5), new CornerRadius(4), new CornerRadius(3), new CornerRadius(5, 5, 0, 0),
        new CornerRadius(3), new Thickness(20), new Thickness(14, 12, 14, 4), new Thickness(16),
        new Thickness(14), new Thickness(0, 0, 0, 8), new Thickness(0, 0, 0, 14));
    private static readonly AppearanceTypography StickyNotesTypography = new(
        24, FontWeights.SemiBold, 14, FontWeights.SemiBold, 14, FontWeights.SemiBold,
        14, FontWeights.SemiBold, 14, 12);
    private static readonly AppearanceTypography MinimalTypography = new(
        22, FontWeights.SemiBold, 13, FontWeights.SemiBold, 13, FontWeights.SemiBold,
        13, FontWeights.SemiBold, 13, 11);
    private static readonly AppearanceTypography SoftTypography = new(
        24, FontWeights.Medium, 15, FontWeights.SemiBold, 15, FontWeights.Medium,
        15, FontWeights.Medium, 14, 12);
    private static readonly AppearanceTypography StudioTypography = new(
        24, FontWeights.Bold, 14, FontWeights.SemiBold, 14, FontWeights.SemiBold,
        14, FontWeights.SemiBold, 14, 12);

    private static readonly AppearancePalette Light = Create(
        "#FFF7F7F7", "#FFF4F6F9", "#FFFFFFFF", "#FF242424", "#FF616161", "#FF20252B",
        "#FF49515A", "#FFE0E0E0",
        "#10000000", "#20000000", "#FF8B1F1F", "#FF446DA5", "#FF005FB8", "#D9F9F9F9");
    private static readonly AppearancePalette Dark = Create(
        "#FF202020", "#FF202020", "#FF2D2D2D", "#FFF3F3F3", "#FFBDBDBD", "#FFF3F3F3",
        "#FFBDBDBD", "#FF454545",
        "#16FFFFFF", "#28FFFFFF", "#FFFFB4AB", "#FF91B8EE", "#FF91B8EE", "#D92C2C2C");
    private static readonly AppearancePalette MinimalLight = Create(
        "#FFF9FAFC", "#FFF5F8FC", "#FFFFFFFF", "#FF1A2533", "#FF64748B", "#FF1A2533",
        "#FF5B6778", "#FFD7DFEA", "#FFEAF2FC", "#FFD8E8FB", "#FF8B1F1F", "#FF2878D0",
        "#FF2878D0", "#FFF9FAFC");
    private static readonly AppearancePalette MinimalDark = Create(
        "#FF171C23", "#FF171C23", "#FF222A35", "#FFE7EDF5", "#FFA6B3C2", "#FFE7EDF5",
        "#FFA6B3C2", "#FF354253", "#FF2C3A4A", "#FF314961", "#FFFFB4AB", "#FF76B6FF",
        "#FF76B6FF", "#FF222A35");
    private static readonly AppearancePalette SoftLight = Create(
        "#FFF8F6F2", "#FFF3F0EA", "#FFFFFCF7", "#FF2A2824", "#FF746B5F", "#FF282722",
        "#FF6F685E", "#FFE1D8CA", "#FFF2EAE0", "#FFEAE2D6", "#FF8B3A30", "#FF557F70",
        "#FF557F70", "#FFFFFCF7");
    private static readonly AppearancePalette SoftDark = Create(
        "#FF2B2A27", "#FF252421", "#FF35332E", "#FFF2EEE4", "#FFC5BBAA", "#FFF2EEE4",
        "#FFC5BBAA", "#FF585247", "#FF3D3932", "#FF474239", "#FFFFB4AB", "#FF96C8B2",
        "#FF96C8B2", "#FF302E29");
    private static readonly AppearancePalette StudioLight = Create(
        "#FFE9EBF1", "#FFF0F1F7", "#FFFFFFFF", "#FF182331", "#FF657286", "#FF7046C4",
        "#FF657286", "#FFD5D5E4", "#FFEFEAFB", "#FFE8E1F8", "#FF8B1F1F", "#FF7652CE",
        "#FF7652CE", "#FFFCFCFE");
    private static readonly AppearancePalette StudioDark = Create(
        "#FF252431", "#FF1B1A23", "#FF292835", "#FFF1EFF7", "#FFB8B5C5", "#FFBBA5FF",
        "#FFB8B5C5", "#FF494657", "#FF332F43", "#FF3B3451", "#FFFFB4AB", "#FFBBA5FF",
        "#FFBBA5FF", "#FF272633");

    public static AppearancePalette Resolve(string theme, string style = UiStyleCatalog.StickyNotes, bool? systemIsDark = null)
    {
        var dark = NotesAppearance.IsDarkTheme(theme, systemIsDark);
        return UiStyleCatalog.Canonicalize(style) switch
        {
            UiStyleCatalog.Minimal => dark ? MinimalDark : MinimalLight,
            UiStyleCatalog.Soft => dark ? SoftDark : SoftLight,
            UiStyleCatalog.Studio => dark ? StudioDark : StudioLight,
            _ => dark ? Dark : Light
        };
    }

    public static void Apply(FrameworkElement surface, AppearancePalette palette, string style)
    {
        var styleId = UiStyleCatalog.Canonicalize(style);
        var metrics = styleId switch
        {
            UiStyleCatalog.Minimal => MinimalMetrics,
            UiStyleCatalog.Soft => SoftMetrics,
            UiStyleCatalog.Studio => StudioMetrics,
            _ => StickyNotesMetrics
        };
        var typography = styleId switch
        {
            UiStyleCatalog.Minimal => MinimalTypography,
            UiStyleCatalog.Soft => SoftTypography,
            UiStyleCatalog.Studio => StudioTypography,
            _ => StickyNotesTypography
        };
        SetBrush("AppWindowBrush", palette.Window);
        SetBrush("AppSettingsBrush", palette.Settings);
        SetBrush("AppCardBrush", palette.Card);
        SetBrush("AppTextBrush", palette.Text);
        SetBrush("AppSecondaryTextBrush", palette.SecondaryText);
        SetBrush("AppSettingsTextBrush", palette.SettingsText);
        SetBrush("AppSettingsSecondaryTextBrush", palette.SettingsSecondaryText);
        SetBrush("AppBorderBrush", palette.Border);
        SetBrush("AppHoverBrush", palette.Hover);
        SetBrush("AppSelectedBrush", palette.Selected);
        SetBrush("AppErrorBrush", palette.Error);
        SetBrush("AppAccentBrush", palette.Accent);
        SetBrush("AppFocusBrush", palette.Focus);
        SetBrush("AppMenuBrush", palette.Menu);
        surface.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "AppTextBrush");
        if (surface is System.Windows.Controls.Control control)
        {
            control.SetResourceReference(System.Windows.Controls.Control.FontFamilyProperty, "AppFontFamily");
            control.SetResourceReference(System.Windows.Controls.Control.FontSizeProperty, "AppBodyFontSize");
        }

        surface.Resources["AppWindowRadius"] = metrics.WindowRadius;
        surface.Resources["AppCardRadius"] = metrics.CardRadius;
        surface.Resources["AppControlRadius"] = metrics.ControlRadius;
        surface.Resources["AppNoteHeaderRadius"] = metrics.NoteHeaderRadius;
        surface.Resources["AppCheckboxRadius"] = metrics.CheckboxRadius;
        surface.Resources["AppWindowPadding"] = metrics.WindowPadding;
        surface.Resources["AppSettingsCardPadding"] = metrics.SettingsCardPadding;
        surface.Resources["AppSettingsPanelPadding"] = metrics.SettingsPanelPadding;
        surface.Resources["AppListCardPadding"] = metrics.ListCardPadding;
        surface.Resources["AppListCardSpacing"] = metrics.ListCardSpacing;
        surface.Resources["AppSectionSpacing"] = metrics.SectionSpacing;
        surface.Resources["AppFontFamily"] = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI");
        surface.Resources["AppBodyFontSize"] = 14d;
        surface.Resources["AppSmallFontSize"] = 11d;
        surface.Resources["AppSettingsHeadingFontSize"] = typography.SettingsHeadingSize;
        surface.Resources["AppSettingsHeadingFontWeight"] = typography.SettingsHeadingWeight;
        surface.Resources["AppSectionHeadingFontSize"] = typography.SectionHeadingSize;
        surface.Resources["AppSectionHeadingFontWeight"] = typography.SectionHeadingWeight;
        surface.Resources["AppNoteTitleFontSize"] = typography.NoteTitleSize;
        surface.Resources["AppNoteTitleFontWeight"] = typography.NoteTitleWeight;
        surface.Resources["AppListTitleFontSize"] = typography.ListTitleSize;
        surface.Resources["AppListTitleFontWeight"] = typography.ListTitleWeight;
        surface.Resources["AppListPreviewFontSize"] = typography.ListPreviewSize;
        surface.Resources["AppListSecondaryFontSize"] = typography.ListSecondarySize;

        // Keep the established Notes resource names shared by menus and detached popups.
        SetBrush("NotesSurfaceBrush", palette.Window);
        SetBrush("NotesCardBrush", palette.Card);
        SetBrush("NotesTextBrush", palette.Text);
        SetBrush("NotesSecondaryTextBrush", palette.SecondaryText);
        SetBrush("NotesBorderBrush", palette.Border);
        SetBrush("NotesHoverBrush", palette.Hover);
        SetBrush("NotesSelectedBrush", palette.Selected);
        SetBrush("NotesErrorBrush", palette.Error);
        SetBrush("NotesMenuBrush", palette.Menu);

        // Fluent submenu templates resolve these resources in detached popup trees.
        surface.Resources["FlyoutBackground"] = surface.Resources["NotesMenuBrush"];
        surface.Resources["FlyoutBorderBrush"] = surface.Resources["NotesBorderBrush"];
        surface.Resources["ContextMenuBackground"] = surface.Resources["NotesMenuBrush"];
        surface.Resources["ComboBoxDropDownBackground"] = surface.Resources["NotesMenuBrush"];
        surface.Resources["CheckBoxBackground"] = surface.Resources["NotesCardBrush"];
        surface.Resources["CheckBoxBorderBrush"] = surface.Resources["NotesSecondaryTextBrush"];

        void SetBrush(string key, MediaColor color) => surface.Resources[key] = new SolidColorBrush(color);
    }

    private static AppearancePalette Create(params string[] colors) => new(
        Parse(colors[0]), Parse(colors[1]), Parse(colors[2]), Parse(colors[3]), Parse(colors[4]),
        Parse(colors[5]), Parse(colors[6]), Parse(colors[7]), Parse(colors[8]), Parse(colors[9]),
        Parse(colors[10]), Parse(colors[11]), Parse(colors[12]), Parse(colors[13]));

    private static MediaColor Parse(string value) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
}

internal static class TrayMenuAppearance
{
    public static void Apply(Forms.ContextMenuStrip menu, string theme, string style = UiStyleCatalog.StickyNotes, bool? systemIsDark = null)
    {
        var palette = ApplicationAppearance.Resolve(theme, style, systemIsDark);
        menu.BackColor = Opaque(palette.Menu);
        menu.ForeColor = Opaque(palette.Text);
        menu.RenderMode = Forms.ToolStripRenderMode.Professional;
        menu.Renderer = new Forms.ToolStripProfessionalRenderer(new HoodColorTable(palette));
    }

    private static System.Drawing.Color Opaque(MediaColor color) =>
        System.Drawing.Color.FromArgb(255, color.R, color.G, color.B);

    private sealed class HoodColorTable(AppearancePalette palette) : Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground => Opaque(palette.Menu);
        public override System.Drawing.Color ToolStripBorder => Opaque(palette.Border);
        public override System.Drawing.Color MenuBorder => Opaque(palette.Border);
        public override System.Drawing.Color MenuItemSelected => Opaque(palette.Hover);
        public override System.Drawing.Color MenuItemSelectedGradientBegin => Opaque(palette.Hover);
        public override System.Drawing.Color MenuItemSelectedGradientEnd => Opaque(palette.Hover);
        public override System.Drawing.Color MenuItemBorder => Opaque(palette.Border);
        public override System.Drawing.Color ImageMarginGradientBegin => Opaque(palette.Menu);
        public override System.Drawing.Color ImageMarginGradientMiddle => Opaque(palette.Menu);
        public override System.Drawing.Color ImageMarginGradientEnd => Opaque(palette.Menu);
        public override System.Drawing.Color SeparatorDark => Opaque(palette.Border);
        public override System.Drawing.Color SeparatorLight => Opaque(palette.Border);
        public override System.Drawing.Color CheckBackground => Opaque(palette.Selected);
        public override System.Drawing.Color CheckSelectedBackground => Opaque(palette.Selected);
    }
}
