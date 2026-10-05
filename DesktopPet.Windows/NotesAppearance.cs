using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using DesktopPet.Core;
using Microsoft.Win32;

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
        surface.Resources["NotesCodeBrush"] = new System.Windows.Media.SolidColorBrush(
            dark ? System.Windows.Media.Color.FromArgb(45, 255, 255, 255) : System.Windows.Media.Color.FromArgb(24, 0, 0, 0));
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
