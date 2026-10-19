# Customize the shared UI styles

For user-facing choices and preview behavior, see [UI styles](ui-styles.md). The shared implementation uses existing palette, metrics, and typography tokens.

## Style ownership

Style values live in [ApplicationAppearance.cs](../DesktopPet.Windows/ApplicationAppearance.cs):

- AppearancePalette defines window, settings, card, text, border, accent, focus, hover, selection, error, and menu colors for light and dark modes.
- AppearanceMetrics defines window/card/control/note-header shapes and the shared window, panel, list, and section spacing.
- AppearanceTypography defines the settings heading, section heading, note title, and list text sizes and weights. Note editor text size continues to come from Notes Settings and is not overridden by an app style.

The common WPF resources and templates in [NotesStyles.xaml](../DesktopPet.Windows/NotesStyles.xaml) consume these tokens. The tray adapter in ApplicationAppearance.cs uses the same palette. Note fills are defined centrally in NoteColorPalette in Core; each style provides five distinct light and dark fills in the stable yellow, blue, green, pink, lavender category order.

## Change a style

Edit the existing tokens for a palette or spacing adjustment. To add or rename a style, update UiStyleCatalog, the palettes and metrics/typography in ApplicationAppearance, NoteColorPalette, the Hood Settings selector, and the configuration and Windows style tests together. Keep existing persisted identifiers readable; `ui_style` in configuration is a data contract. Brightness continues to use `notes.theme`.

Use the shared resources across windows, menus, and the tray so a live preview updates every surface. Preserve saved note-color categories, content, formatting, and editor text size. Save persists the previewed choices; Cancel or closing Settings restores the previous appearance.

## Verify

Run `tools\Test.ps1`, then check light, dark, and system themes, all five note colors, focused/unfocused notes, menus, and Settings Save/Cancel in a packaged build. See [build and development](build.md) for packaging.
