using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using DesktopPet.Core;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopPet.Windows;

internal static class NotesAppearance
{
    public static bool Apply(FrameworkElement surface, string theme, string style = UiStyleCatalog.StickyNotes, bool? systemIsDark = null)
    {
        var styles = surface.Resources.MergedDictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.EndsWith("NotesStyles.xaml", StringComparison.OrdinalIgnoreCase) == true);
        if (styles is null)
        {
            styles = new ResourceDictionary { Source = new Uri("/DesktopPet.Windows;component/NotesStyles.xaml", UriKind.Relative) };
            surface.Resources.MergedDictionaries.Add(styles);
        }

        var dark = IsDarkTheme(theme, systemIsDark);
        var palette = ApplicationAppearance.Resolve(theme, style, systemIsDark);
        var mode = dark ? ".Dark" : ".Light";
        var source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent{mode}.xaml");
        if (styles.MergedDictionaries[0].Source != source)
        {
            styles.MergedDictionaries[0] = new ResourceDictionary { Source = source };
        }

        if (surface is Window window)
        {
#pragma warning disable WPF0001 // WPF's built-in Fluent theme API is experimental in .NET 10.
            window.ThemeMode = theme.ToLowerInvariant() switch { "light" => ThemeMode.Light, "dark" => ThemeMode.Dark, _ => ThemeMode.System };
#pragma warning restore WPF0001
        }
        else if (surface is System.Windows.Controls.ContextMenu menu)
        {
            menu.SetResourceReference(FrameworkElement.StyleProperty, "NotesMenu");
            menu.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI");
            menu.FontSize = 14;
        }

        ApplicationAppearance.Apply(surface, palette, style);
        if (surface is NoteWindow noteWindow && WindowChrome.GetWindowChrome(noteWindow) is { } chrome)
        {
            if (chrome.IsFrozen)
            {
                chrome = (WindowChrome)chrome.Clone();
                WindowChrome.SetWindowChrome(noteWindow, chrome);
            }

            chrome.CornerRadius = (CornerRadius)noteWindow.Resources["AppWindowRadius"];
        }

        return dark;
    }

    public static void ApplyContent(FrameworkElement surface, bool dark, NoteColor color, string style)
    {
        var palette = ApplicationAppearance.Resolve(dark ? "dark" : "light", style);
        var note = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(NoteColorPalette.GetBackgroundHex(color, dark, style));
        var accent = style switch { UiStyleCatalog.Minimal => .06, UiStyleCatalog.Soft => .10, UiStyleCatalog.Studio => .18, _ => 0 };
        var tint = Mix(note, palette.Accent, accent);
        // Share the note/style hue while preserving the contrast checked by the content tests.
        var surfaceTone = dark ? MediaColors.Black : MediaColors.White;
        var edgeTone = dark ? MediaColors.White : MediaColors.Black;
        SetBrush("NotesCodeBrush", Mix(tint, surfaceTone, dark ? .55 : .40));
        SetBrush("NotesInlineCodeBrush", Mix(tint, MediaColors.Black, dark ? .55 : .24));
        SetBrush("NotesTableBrush", Mix(tint, surfaceTone, dark ? .35 : .75));
        SetBrush("NotesTableHeaderBrush", Mix(tint, edgeTone, dark ? .14 : .18));
        SetBrush("NotesContentBorderBrush", Mix(tint, edgeTone, dark ? .70 : .64));
        SetBrush("NotesLinkBrush", Mix(palette.Accent, tint, dark ? .12 : .08));
        var padding = style switch { UiStyleCatalog.Minimal => 4, UiStyleCatalog.Soft => 8, _ => 6 };
        surface.Resources["NotesTableCellPadding"] = new Thickness(padding);
        surface.Resources["NotesCodePadding"] = new Thickness(padding + 2);

        void SetBrush(string key, MediaColor value) => surface.Resources[key] = new SolidColorBrush(value);
    }

    private static MediaColor Mix(MediaColor first, MediaColor second, double amount) => MediaColor.FromRgb(
        Blend(first.R, second.R, amount), Blend(first.G, second.G, amount), Blend(first.B, second.B, amount));

    private static byte Blend(byte first, byte second, double amount) => (byte)Math.Round(first * (1 - amount) + second * amount);

    internal static bool IsDarkTheme(string theme, bool? systemIsDark = null)
    {
        if (theme.Equals("dark", StringComparison.OrdinalIgnoreCase)) return true;
        if (theme.Equals("light", StringComparison.OrdinalIgnoreCase)) return false;
        if (systemIsDark.HasValue) return systemIsDark.Value;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int lightTheme && lightTheme == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
