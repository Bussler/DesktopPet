# Customize the shared UI styles

Style values live in C# in DesktopPet.Windows/ApplicationAppearance.cs:

- AppearancePalette defines window, settings, card, text, border, accent, focus, hover, selection, error, and menu colors for light and dark modes.
- AppearanceMetrics defines window/card/control/note-header shapes and the shared window, panel, list, and section spacing.
- AppearanceTypography defines the settings heading, section heading, note title, and list text sizes and weights. Note editor text size continues to come from Notes Settings and is not overridden by an app style.

The common WPF resources and templates in DesktopPet.Windows/NotesStyles.xaml consume these tokens. The tray adapter in ApplicationAppearance.cs uses the same palette. Note fills are defined centrally in NoteColorPalette in Core; each style provides five distinct light and dark fills in the stable yellow, blue, green, pink, lavender category order. To add or rename a style, update UiStyleCatalog, the palettes and metrics/typography in ApplicationAppearance, NoteColorPalette, the Hood Settings selector, and the configuration and Windows style tests together.
