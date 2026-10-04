# UI styles

Choose Sticky Notes, Minimal, Soft, or Studio in Hood Settings and save. The choice applies across notes and their menus, the Notes list and Trash, Hood's menus, both settings windows, nested menus and dropdowns, and the tray menu. A successful save updates open surfaces. If Hood reports an appearance-refresh problem, restart to apply the saved style.

Brightness stays a separate app-wide choice in Notes Settings: System follows Windows, while Light and Dark remain fixed. Changing a style or brightness preserves each note's saved color category, content, formatting, saved text size, window behavior, and Hood's session. The displayed fill changes with the selected style and brightness.

| Style | Direction |
| --- | --- |
| Sticky Notes | Current design and compatibility default |
| Minimal | Compact spacing, restrained corners, quiet borders, and blue accents |
| Soft | Warm surfaces, rounder shapes, gentler spacing, and green accents |
| Studio | Crisp panels, restrained purple edges, and stronger heading, focus, and selection emphasis |

The configuration stores the style as ui_style in %LOCALAPPDATA%\HoodDesktopPet\config.yaml. Supported values are sticky-notes, minimal, soft, and studio; the default is sticky-notes. Other identifiers are invalid. The notes.theme value remains the only brightness setting.

## Customize the shared definitions

Style values live in C# in DesktopPet.Windows/ApplicationAppearance.cs:

- AppearancePalette defines window, settings, card, text, border, accent, focus, hover, selection, error, and menu colors for light and dark modes.
- AppearanceMetrics defines window/card/control/note-header shapes and the shared window, panel, list, and section spacing.
- AppearanceTypography defines the settings heading, section heading, note title, and list text sizes and weights. Note editor text size continues to come from Notes Settings and is not overridden by an app style.

The common WPF resources and templates in DesktopPet.Windows/NotesStyles.xaml consume these tokens. The tray adapter in ApplicationAppearance.cs uses the same palette. Note fills are defined centrally in NoteColorPalette in Core; each style provides five distinct light and dark fills in the stable yellow, blue, green, pink, lavender category order. To add or rename a style, update UiStyleCatalog, the palettes and metrics/typography in ApplicationAppearance, NoteColorPalette, the Hood Settings selector, and the configuration and Windows style tests together.
