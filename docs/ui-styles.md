# UI styles

Choose Sticky Notes, Minimal, Soft, or Studio in Hood Settings to preview it immediately across notes and their menus, the Notes list and Trash, Hood's menus, both settings windows, nested menus and dropdowns, and the tray menu. **Save** keeps the choice for the next launch. **Cancel** or closing Settings restores the previous UI style and pet. Previewing and restoring styles leave configuration and note data untouched. If a preview fails, Hood restores the previous appearance and reports an error.

Brightness is an app-wide choice beside UI style in Hood Settings: System follows Windows, while Light and Dark remain fixed. It previews immediately; Save keeps the choice and Cancel or closing Settings restores the previous brightness. Changing a style or brightness preserves each note's saved color category, content, formatting, saved text size, window behavior, and Hood's session. The displayed fill changes with the selected style and brightness.

| Style | Direction |
| --- | --- |
| Sticky Notes | Current design and compatibility default |
| Minimal | Compact spacing, restrained corners, quiet borders, and blue accents |
| Soft | Warm surfaces, rounder shapes, gentler spacing, and green accents |
| Studio | Crisp panels, restrained purple edges, and stronger heading, focus, and selection emphasis |

The configuration stores the style as ui_style in %LOCALAPPDATA%\HoodDesktopPet\config.yaml. Supported values are sticky-notes, minimal, soft, and studio; the default is sticky-notes. Other identifiers are invalid. The notes.theme value remains the only brightness setting.
